using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The answer writer seam for a Q&A round (SW-9 Q3): IRoundAnswerWriter is SW-6's batching AnswerWriter, so a guest's
// upvotes are stored, refused as stale or refused as closed exactly as a slide poll's answer is, and the real tally counts
// them by option position, padded for a question added since and rebuilt from the rows by a tally that has never seen the
// round. SW-6's poll pump never takes a Q&A round's change. Q&A rounds and their options are written by PollRowSeed in a
// deck PollSeed made live; the writer doesn't check the roster, so guests are bare sw_guest ids. The app's database is
// watched at the EF boundary so a test knows when the writer's transaction begins. The app is shared by the fixture, so
// every test seeds a deck, a round and guests of its own.
[TestFixture]
[CancelAfter(60000)]
public class RoundAnswerWriterTests : PollTestFixture
{
    private const int SLIDE = 3;

    private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(30);

    private readonly TransactionWatch _starts = new();

    private IRoundAnswerWriter Writer => Factory.Services.GetRequiredService<IRoundAnswerWriter>();

    private IPollTallies Tallies => Factory.Services.GetRequiredService<IPollTallies>();

    protected override void ConfigurePollServices(IServiceCollection services)
        => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(_starts));

    [Test]
    public async Task ReplaceAsync_WhenRoundIsQa_ShouldStoreThePicksAndRaiseTheRevision()
    {
        var (target, round) = await SeedQaRoundAsync(3);
        var guest = GuestCookie.NewId();

        var first = await BoundedAsync(Writer.ReplaceAsync(target, guest, [0, 2], 0));
        var second = await BoundedAsync(Writer.ReplaceAsync(target, guest, [1, 2], 1));
        var stored = await AnswerRowAsync(round.Id, guest);
        var tally = await Tallies.ReadAsync(target.RoundId, 3);

        var firstWritten = first.ShouldBeOfType<RoundWrite.Written>();
        firstWritten.Picks.ShouldBe([0, 2]);
        firstWritten.Revision.ShouldBe(1);
        var secondWritten = second.ShouldBeOfType<RoundWrite.Written>();
        secondWritten.Picks.ShouldBe([1, 2]);
        secondWritten.Revision.ShouldBe(2);
        stored.ShouldNotBeNull();
        stored.Picks.ShouldBe([1, 2]);
        stored.Revision.ShouldBe(2);
        tally.Answered.ShouldBe(1);
        tally.Counts.ShouldBe([0, 1, 1]);
    }

    [Test]
    public async Task ReplaceAsync_WhenRevisionOld_ShouldBeStale()
    {
        var (target, round) = await SeedQaRoundAsync(2);
        var guest = GuestCookie.NewId();
        await BoundedAsync(Writer.ReplaceAsync(target, guest, [0], 0));

        var stale = await BoundedAsync(Writer.ReplaceAsync(target, guest, [1], 0));
        var stored = await AnswerRowAsync(round.Id, guest);
        var tally = await Tallies.ReadAsync(target.RoundId, 2);

        stale.ShouldBeOfType<RoundWrite.Stale>();
        stored.ShouldNotBeNull();
        stored.Picks.ShouldBe([0]);
        stored.Revision.ShouldBe(1);
        tally.Counts.ShouldBe([1, 0]);
    }

    [Test]
    public async Task ReplaceAsync_WhenRoundClosedBeforeCommit_ShouldBeClosedAndWriteNothing()
    {
        var (target, round) = await SeedQaRoundAsync(2);
        var guest = GuestCookie.NewId();

        // Hold SQLite's write lock (BEGIN IMMEDIATE), so the writer's group can't begin until the round is closed under it.
        Task<RoundWrite> upvote;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var close = await db.Database.BeginTransactionAsync();
            var writerBegins = _starts.NextStart();
            upvote = Writer.ReplaceAsync(target, guest, [1], 0);
            await BoundedAsync(writerBegins);
            await CloseAsync(db, round.Id);
            await close.CommitAsync();
        }
        var result = await BoundedAsync(upvote);
        var stored = await AnswerRowAsync(round.Id, guest);

        result.ShouldBeOfType<RoundWrite.Closed>();
        stored.ShouldBeNull();
    }

    [Test]
    public async Task ReadAsync_WhenAQuestionWasAddedAfterTheFirstRead_ShouldPadTheCounts()
    {
        var (target, round) = await SeedQaRoundAsync(2);
        await BoundedAsync(Writer.ReplaceAsync(target, GuestCookie.NewId(), [0, 1], 0));

        var before = await Tallies.ReadAsync(target.RoundId, 2);
        await Rows.OptionAsync(round.Id, "Question 3");
        var added = await Tallies.ReadAsync(target.RoundId, 3);
        await BoundedAsync(Writer.ReplaceAsync(target with { OptionCount = 3 }, GuestCookie.NewId(), [2], 0));
        var voted = await Tallies.ReadAsync(target.RoundId, 3);

        before.Counts.ShouldBe([1, 1]);
        added.Answered.ShouldBe(1);
        added.Counts.ShouldBe([1, 1, 0]);
        voted.Answered.ShouldBe(2);
        voted.Counts.ShouldBe([1, 1, 1]);
    }

    // A tally made from the app's services after the rows were written stands in for the app after a restart.
    [Test]
    public async Task ReadAsync_WhenRestartedOnAQaRound_ShouldRebuildFromRows()
    {
        var (target, round) = await SeedQaRoundAsync(3);
        await BoundedAsync(Writer.ReplaceAsync(target, GuestCookie.NewId(), [0, 2], 0));
        await BoundedAsync(Writer.ReplaceAsync(target, GuestCookie.NewId(), [2], 0));
        var changed = GuestCookie.NewId();
        await BoundedAsync(Writer.ReplaceAsync(target, changed, [0], 0));
        await BoundedAsync(Writer.ReplaceAsync(target, changed, [1], 1));
        await Rows.OptionAsync(round.Id, "Question 4");
        var restarted = ActivatorUtilities.CreateInstance<PollTallies>(Factory.Services);

        var rebuilt = await restarted.ReadAsync(target.RoundId, 4);
        var running = await Tallies.ReadAsync(target.RoundId, 4);

        rebuilt.Answered.ShouldBe(3);
        rebuilt.Counts.ShouldBe([1, 1, 2, 0]);
        running.Answered.ShouldBe(rebuilt.Answered);
        running.Counts.ShouldBe(rebuilt.Counts);
    }

    // A pump made from the app's services over a tally and a publisher of the test's own: a Q&A round's change, raised
    // first, must neither throw nor publish; the slide round's change after it on the same deck is the only publish.
    [Test]
    public async Task Changed_WhenRoundIsQa_ShouldNotReachThePollPump()
    {
        var deck = $"deck-{Guid.NewGuid():N}";
        var tallies = new FakePollTallies();
        var publisher = new RecordingPublisher();
        using var pump = ActivatorUtilities.CreateInstance<PollTallyPump>(Factory.Services, tallies, publisher);
        await BoundedAsync(pump.StartAsync(CancellationToken.None));

        await tallies.ApplyAsync(new PollRoundRef(deck, PollKind.Qa, null, null, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), 2), [], [0]);
        await tallies.ApplyAsync(new PollRoundRef(deck, PollKind.Slide, SLIDE, Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), 3), [], [0]);
        await BoundedAsync(publisher.First);
        await BoundedAsync(pump.StopAsync(CancellationToken.None));

        publisher.Published.ShouldBe([(deck, SLIDE)]);
    }

    // An open Q&A round in a live deck of its own, with this many questions, and the reference the writer takes for it.
    private async Task<(PollRoundRef Target, PollRound Round)> SeedQaRoundAsync(int questions)
    {
        var poll = await SeedPollAsync();
        var round = await Rows.QaRoundAsync(poll.DeckId, poll.SessionId);
        for (var number = 1; number <= questions; number++)
        {
            await Rows.OptionAsync(round.Id, $"Question {number}");
        }
        var target = new PollRoundRef(poll.DeckId, PollKind.Qa, null, null, poll.SessionId.ToString(), round.Id.ToString(), questions);
        return (target, round);
    }

    private async Task<PollAnswer?> AnswerRowAsync(Guid roundId, string guestId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PollAnswers.AsNoTracking().SingleOrDefaultAsync(a => a.RoundId == roundId && a.GuestId == guestId);
    }

    // Closes the Q&A round inside the caller's transaction, as closing a round does: ClosedAt, and Final from the options
    // and the answer rows.
    private async Task CloseAsync(AppDbContext db, Guid roundId)
    {
        var round = await db.PollRounds.SingleAsync(r => r.Id == roundId);
        var options = await db.PollOptions.AsNoTracking().Where(o => o.RoundId == roundId).OrderBy(o => o.Position).Select(o => o.Text).ToListAsync();
        var answers = await db.PollAnswers.AsNoTracking().Where(a => a.RoundId == roundId).Select(a => a.Picks).ToListAsync();
        round.ClosedAt = Clock.GetUtcNow();
        round.JoinedAtClose = answers.Count;
        round.Final = new PollFinal(options, [.. options.Select((_, position) => answers.Count(picks => picks.Contains(position)))], answers.Count);
        await db.SaveChangesAsync();
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

    // Records each publish as (deck, slide); First completes on the first.
    private sealed class RecordingPublisher : IPollPublisher
    {
        private readonly Lock _gate = new();
        private readonly List<(string DeckId, int Slide)> _published = [];
        private readonly TaskCompletionSource _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task First => _first.Task;

        public IReadOnlyList<(string DeckId, int Slide)> Published
        {
            get
            {
                lock (_gate)
                {
                    return [.. _published];
                }
            }
        }

        public Task PublishAsync(string deckId, int slide)
        {
            lock (_gate)
            {
                _published.Add((deckId, slide));
            }
            _first.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
