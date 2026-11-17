using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
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

// The batching answer writer against the real tally (PollTallies), not the fake: a round the tally has never seen is
// loaded from its rows before the writer's group writes any, so every answer is counted once, whether a whole group lands
// on it at once or a read first touches it between the writer's commit and its ApplyAsync. Each test compares the tally
// with Final, which closing the round counts from the rows. The app's database is watched at the EF boundary: Saves counts
// each save of a round's answers, and Transactions tells a test when the next transaction begins and runs a step right
// after the next one commits. Guests go on the roster through IAudienceRoster, as JoinAudience does. Every test seeds decks,
// rounds and guests of its own.
[TestFixture]
[CancelAfter(60000)]
public class AnswerTallyTests : PollTestFixture
{
    private const int GROUP = 50;

    private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(30);

    // Long enough for GROUP queued answers to pass their checks in memory over the test server.
    private static readonly TimeSpan QUEUE_TIME = TimeSpan.FromSeconds(1);

    private readonly SaveWatch _saves = new();
    private readonly TransactionWatch _transactions = new();

    private IPollTallies Tallies => Factory.Services.GetRequiredService<IPollTallies>();

    protected override void ConfigurePollServices(IServiceCollection services)
        => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(_saves, _transactions));

    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task Answers_WhenOneGroupLandsOnARoundTheTallyHasNotSeen_ShouldCountEachOnce()
    {
        var blocking = await SeedPollAsync();
        var blockingRound = await Rows.RoundAsync(blocking.SlideId, blocking.SessionId);
        var blocker = (await SeatGuestsAsync(blocking, 1))[0];
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        var guests = await SeatGuestsAsync(poll, GROUP);
        using var http = GuestHttp();

        // Hold SQLite's write lock while the writer is on another round's group, so this round's answers all queue behind it
        // and go in one group.
        Task<HttpResponseMessage> first;
        List<Task<HttpResponseMessage>> answers;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var hold = await db.Database.BeginTransactionAsync();
            var writerBegins = _transactions.NextStart();
            first = AnswerAsync(http, blocking.Code, blocker, new AnswerRequest(blockingRound.Id.ToString(), [0], 0));
            await BoundedAsync(writerBegins);
            answers = [.. guests.Select((guest, i) => AnswerAsync(http, poll.Code, guest, new AnswerRequest(round.Id.ToString(), [i % 3], 0)))];
            await Task.Delay(QUEUE_TIME, CancellationToken.None);
            await hold.RollbackAsync();
        }
        (await BoundedAsync(first)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var responses = await BoundedAsync(Task.WhenAll(answers));
        var tally = await Tallies.ReadAsync(round.Id.ToString(), PollSeed.OPTIONS.Length);
        var final = await CloseInOneTransactionAsync(round.Id);

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        _saves.SavesOf(round.Id).ShouldBeLessThan(GROUP, "the answers should have committed in groups of several");
        final.Answered.ShouldBe(GROUP);
        tally.Answered.ShouldBe(final.Answered);
        tally.Counts.ShouldBe(final.Counts);
    }

    [Test]
    public async Task Answer_WhenAReadFirstTouchesTheRoundBetweenCommitAndApply_ShouldCountItOnce()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        var guest = (await SeatGuestsAsync(poll, 1))[0];
        using var http = GuestHttp();

        var racingRead = _transactions.AfterNextCommit(() => Tallies.ReadAsync(round.Id.ToString(), PollSeed.OPTIONS.Length));
        var response = await BoundedAsync(AnswerAsync(http, poll.Code, guest, new AnswerRequest(round.Id.ToString(), [1], 0)));
        await BoundedAsync(racingRead);
        var tally = await Tallies.ReadAsync(round.Id.ToString(), PollSeed.OPTIONS.Length);
        var final = await CloseInOneTransactionAsync(round.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        final.Answered.ShouldBe(1);
        tally.Answered.ShouldBe(1);
        tally.Counts.ShouldBe([0, 1, 0]);
        tally.Counts.ShouldBe(final.Counts);
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

    // Closes the round as V4's Close does, in one write transaction: ClosedAt, and Final counted from the answer rows.
    private async Task<PollFinal> CloseInOneTransactionAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var round = await db.PollRounds.SingleAsync(r => r.Id == roundId);
        var answers = await db.PollAnswers.AsNoTracking().Where(a => a.RoundId == roundId).Select(a => a.Picks).ToListAsync();
        var counts = PollSeed.OPTIONS.Select((_, position) => answers.Count(picks => picks.Contains(position))).ToList();
        round.ClosedAt = Clock.GetUtcNow();
        round.JoinedAtClose = answers.Count;
        round.Final = new PollFinal(PollSeed.OPTIONS, counts, answers.Count);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
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

    // Counts each save that writes a round's answers.
    private sealed class SaveWatch : SaveChangesInterceptor
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<Guid, int> _saves = [];

        public int SavesOf(Guid roundId)
        {
            lock (_gate)
            {
                return _saves.GetValueOrDefault(roundId);
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
                }
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    // Tells a waiting test when the next transaction begins, and runs a test's step once the next transaction has committed,
    // before the code that committed it goes on.
    private sealed class TransactionWatch : DbTransactionInterceptor
    {
        private TaskCompletionSource? _nextStart;
        private (Func<Task> Step, TaskCompletionSource Done)? _afterCommit;
        private readonly Lock _gate = new();

        public Task NextStart()
        {
            var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _nextStart, next);
            return next.Task;
        }

        // Completes once the step has run after the next commit.
        public Task AfterNextCommit(Func<Task> step)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _afterCommit = (step, done);
            }
            return done.Task;
        }

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Exchange(ref _nextStart, null)?.TrySetResult();
            return base.TransactionStartingAsync(connection, eventData, result, cancellationToken);
        }

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            (Func<Task> Step, TaskCompletionSource Done)? due;
            lock (_gate)
            {
                due = _afterCommit;
                _afterCommit = null;
            }
            if (due is { } run)
            {
                await run.Step();
                run.Done.TrySetResult();
            }
            await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
        }
    }
}
