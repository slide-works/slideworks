using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The batching answer writer (V5, D16) under PUT /api/join/{code}/poll/answer: answers commit in groups, a round that
// closes before an answer's group commits makes that answer stale and uncounted, a burst racing a close never counts an
// answer it refused, and a group that fails to commit fails every waiter and applies nothing to the tally.
//
// The app's database is watched at the EF boundary: Saves counts each save that writes a round's answers and fails those of
// a round a test poisons, and Starts tells a test when the next transaction begins. Many guests are put on the session's
// roster through IAudienceRoster, as the hub's JoinAudience does, without a socket each. Closing a round races the writer
// through CloseInOneTransactionAsync, which closes as V4's Close does, in one write transaction that counts the rows: the
// real close is not in this wave, and PollRowSeed.CloseAsync reads the rows outside its write. The fakes and the app are
// shared by the fixture, so every test seeds a deck, a code, rounds and guests of its own.
[TestFixture]
[CancelAfter(120000)]
public class AnswerWriterTests : PollTestFixture
{
    private const int RACE_ROUNDS = 25;
    private const int RACE_GUESTS = 40;
    private const int BURST = 500;

    private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(30);

    private readonly SaveWatch _saves = new();
    private readonly TransactionWatch _starts = new();

    private FakePollTallies Tallies => Factory.Services.GetRequiredService<FakePollTallies>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakeAudienceActivity.AddTo(services);
        FakePollTallies.AddTo(services);
        services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(_saves, _starts));
    }

    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task Answers_WhenFiveHundredArriveAtOnce_ShouldCommitInFewTransactions()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        var guests = await SeatGuestsAsync(poll, BURST);
        using var http = GuestHttp();

        var responses = await BoundedAsync(Task.WhenAll(guests.Select((guest, i) => AnswerAsync(http, poll.Code, guest, new AnswerRequest(round.Id.ToString(), [i % 3], 0)))));
        var closed = await CloseInOneTransactionAsync(round.Id);

        await TestContext.Out.WriteLineAsync($"{BURST} answers committed in {_saves.SavesOf(round.Id)} transactions");
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        closed.Answered.ShouldBe(BURST);
        _saves.SavesOf(round.Id).ShouldBeLessThanOrEqualTo(BURST / 20, "500 answers should commit in a few groups, not one transaction each");
        Tallies.Applied.Count(a => a.Round.RoundId == round.Id.ToString()).ShouldBe(BURST);
    }

    [Test]
    public async Task Answer_WhenRoundClosesBeforeItsBatchCommits_ShouldBeStaleAndNotCounted()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        var guest = (await SeatGuestsAsync(poll, 1))[0];
        using var http = GuestHttp();

        // Hold SQLite's write lock (BEGIN IMMEDIATE), so the writer's group can't begin until the round is closed under it.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        PollFinal final;
        Task<HttpResponseMessage> answer;
        await using (var close = await db.Database.BeginTransactionAsync())
        {
            var writerBegins = _starts.NextStart();
            answer = AnswerAsync(http, poll.Code, guest, new AnswerRequest(round.Id.ToString(), [1], 0));
            await BoundedAsync(writerBegins);
            final = await CloseAsync(db, round.Id);
            await close.CommitAsync();
        }
        var response = await BoundedAsync(answer);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe("stale");
        final.Answered.ShouldBe(0);
        _saves.SavesOf(round.Id).ShouldBe(0);
        Tallies.Applied.ShouldNotContain(a => a.Round.RoundId == round.Id.ToString());
    }

    [Test]
    public async Task Answers_WhenACloseRacesABurst_ShouldCountExactlyTheAcceptedOnes()
    {
        var poll = await SeedPollAsync();
        var guests = await SeatGuestsAsync(poll, RACE_GUESTS);
        using var http = GuestHttp();
        var (accepted, stale, mixed) = (0, 0, 0);

        for (var number = 1; number <= RACE_ROUNDS; number++)
        {
            var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId, number: number);
            // Odd rounds close from inside the writer's loop, between two of its commits, once a random number of this
            // round's answers has committed: every answer still being checked or queued then races the close. Even rounds
            // close from the test once the first answer has committed, against the writer's next commits.
            var closeAfter = number % 2 == 1 ? Random.Shared.Next(1, RACE_GUESTS) : 1;
            var inLoop = number % 2 == 1;
            var applied = 0;
            var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closed = new TaskCompletionSource<PollFinal>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watch = Tallies.Changed(async changed =>
            {
                if (changed.RoundId != round.Id.ToString() || Interlocked.Increment(ref applied) != closeAfter)
                {
                    return;
                }
                committed.TrySetResult();
                if (inLoop)
                {
                    closed.TrySetResult(await CloseInOneTransactionAsync(round.Id));
                }
            });
            var answers = guests.Select((guest, i) => AnswerAsync(http, poll.Code, guest, new AnswerRequest(round.Id.ToString(), [i % 3], 0))).ToList();
            await BoundedAsync(committed.Task);
            if (!inLoop)
            {
                await Task.Delay(Random.Shared.Next(0, 4), CancellationToken.None);
                closed.TrySetResult(await CloseInOneTransactionAsync(round.Id));
            }
            var final = await BoundedAsync(closed.Task);
            var responses = await BoundedAsync(Task.WhenAll(answers));

            responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict, $"round {number}");
            final.Answered.ShouldBe(responses.Count(r => r.StatusCode == HttpStatusCode.OK), $"round {number}");
            Tallies.Applied.Count(a => a.Round.RoundId == round.Id.ToString()).ShouldBe(final.Answered, $"round {number}");
            accepted += final.Answered;
            stale += responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
            mixed += final.Answered > 0 && final.Answered < RACE_GUESTS ? 1 : 0;
        }

        await TestContext.Out.WriteLineAsync($"{RACE_ROUNDS} races of {RACE_GUESTS}: {accepted} accepted, {stale} stale, {mixed} closed mid-burst");
        mixed.ShouldBeGreaterThanOrEqualTo(4, "closes should land in the middle of a burst");
    }

    [Test]
    public async Task Answer_WhenBatchFails_ShouldFailEveryWaiterAndApplyNothing()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        var guests = await SeatGuestsAsync(poll, 5);
        _saves.Poison(round.Id);

        var failures = await BoundedAsync(Task.WhenAll(guests.Select(guest => Should.ThrowAsync<PoisonedSaveException>(
            () => SubmitAsync(poll.Code, guest, new AnswerRequest(round.Id.ToString(), [1], 0))))));
        _saves.Heal(round.Id);
        var later = await BoundedAsync(SubmitAsync(poll.Code, guests[0], new AnswerRequest(round.Id.ToString(), [2], 0)));

        failures.Length.ShouldBe(5);
        Tallies.Applied.Count(a => a.Round.RoundId == round.Id.ToString()).ShouldBe(1);
        var accepted = later.ShouldBeOfType<AnswerResult.Accepted>("the writer kept going after the failed group");
        accepted.Picks.ShouldBe([2]);
        accepted.Revision.ShouldBe(1, "nothing of the failed group was written");
    }

    // Guests on the live session's roster, each with an sw_guest id of its own.
    private async Task<List<string>> SeatGuestsAsync(SeededPoll poll, int count)
    {
        var roster = Factory.Services.GetRequiredService<IAudienceRoster>();
        var guests = Enumerable.Range(0, count).Select(_ => GuestCookie.NewId()).ToList();
        foreach (var guest in guests)
        {
            await roster.JoinAsync(poll.DeckId, poll.SessionId.ToString(), guest, $"connection-{guest}");
        }
        return guests;
    }

    private HttpClient GuestHttp() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    // PUT /api/join/{code}/poll/answer as this guest, from the guest's page.
    private static Task<HttpResponseMessage> AnswerAsync(HttpClient http, string code, string guestId, AnswerRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/join/{code}/poll/answer") { Content = JsonContent.Create(request) };
        message.Headers.Add("Origin", TestAppFactory.PUBLIC_URL);
        message.Headers.Add("Cookie", $"{GuestCookie.NAME}={guestId}");
        return http.SendAsync(message);
    }

    // The seam itself, for a failure the route would turn into an unhandled exception.
    private async Task<AnswerResult> SubmitAsync(string code, string guestId, AnswerRequest request)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPollAnswers>().SubmitAsync(code, guestId, request);
    }

    private async Task<PollFinal> CloseInOneTransactionAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var final = await CloseAsync(db, roundId);
        await transaction.CommitAsync();
        return final;
    }

    // Closes the round inside the caller's transaction as V4's Close does: ClosedAt, and Final counted from the answer rows.
    private async Task<PollFinal> CloseAsync(AppDbContext db, Guid roundId)
    {
        var round = await db.PollRounds.SingleAsync(r => r.Id == roundId);
        var answers = await db.PollAnswers.AsNoTracking().Where(a => a.RoundId == roundId).Select(a => a.Picks).ToListAsync();
        var counts = PollSeed.OPTIONS.Select((_, position) => answers.Count(picks => picks.Contains(position))).ToList();
        round.ClosedAt = Clock.GetUtcNow();
        round.JoinedAtClose = answers.Count;
        round.Final = new PollFinal(PollSeed.OPTIONS, counts, answers.Count);
        await db.SaveChangesAsync();
        return round.Final;
    }

    // The task's result, failing the test when it doesn't come within WAIT rather than hanging it.
    private static async Task<T> BoundedAsync<T>(Task<T> task)
    {
        var first = await Task.WhenAny(task, Task.Delay(WAIT, CancellationToken.None));
        first.ShouldBeSameAs(task, $"no result within {WAIT}");
        return await task;
    }

    private static async Task BoundedAsync(Task task)
    {
        var first = await Task.WhenAny(task, Task.Delay(WAIT, CancellationToken.None));
        first.ShouldBeSameAs(task, $"not done within {WAIT}");
        await task;
    }

    // Counts each save that writes a round's answers, and throws on a save that writes a poisoned round's answers.
    private sealed class SaveWatch : SaveChangesInterceptor
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<Guid, int> _saves = [];
        private readonly HashSet<Guid> _poisoned = [];

        public int SavesOf(Guid roundId)
        {
            lock (_gate)
            {
                return _saves.GetValueOrDefault(roundId);
            }
        }

        public void Poison(Guid roundId)
        {
            lock (_gate)
            {
                _poisoned.Add(roundId);
            }
        }

        public void Heal(Guid roundId)
        {
            lock (_gate)
            {
                _poisoned.Remove(roundId);
            }
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var rounds = eventData.Context!.ChangeTracker.Entries<PollAnswer>().Select(e => e.Entity.RoundId).Distinct().ToList();
            lock (_gate)
            {
                foreach (var round in rounds)
                {
                    _saves[round] = _saves.GetValueOrDefault(round) + 1;
                    if (_poisoned.Contains(round))
                    {
                        throw new PoisonedSaveException();
                    }
                }
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    // Tells a waiting test when the next database transaction begins.
    private sealed class TransactionWatch : DbTransactionInterceptor
    {
        private TaskCompletionSource? _next;

        public Task NextStart()
        {
            var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _next, next);
            return next.Task;
        }

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Exchange(ref _next, null)?.TrySetResult();
            return base.TransactionStartingAsync(connection, eventData, result, cancellationToken);
        }
    }

    public sealed class PoisonedSaveException : Exception
    {
        public PoisonedSaveException()
            : base("A test failed this save.")
        {
        }
    }
}
