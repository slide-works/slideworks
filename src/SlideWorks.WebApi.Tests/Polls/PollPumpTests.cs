using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The pump between tally changes and the publisher (V5): FakePollTallies raises Changed as the real tally does after an
// answer, and FakePollPublisher records each publish. The fixture's fake clock drives the half second, so nothing here
// sleeps: a publish that the clock allows runs on the thread pool, and the tests wait for it with a bounded wait. The app
// is shared by the fixture, so every test uses a deck of its own. The start-up tests run a second pump from the app's
// services over a recording tally of their own, so the app's pump and its subscription are left alone.
[TestFixture]
[CancelAfter(60000)]
public class PollPumpTests : PollTestFixture
{
    private const int SLIDE = 3;

    private static readonly TimeSpan _justBefore = PollTallyPump.Interval - TimeSpan.FromMilliseconds(1);

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakePollTallies.AddTo(services);
        FakePollPublisher.AddTo(services);
        services.AddSingleton<WatchedPublisher>().AddSingleton<IPollPublisher>(provider => provider.GetRequiredService<WatchedPublisher>());
    }

    private string _deck = null!;

    private FakePollTallies Tallies => Factory.Services.GetRequiredService<FakePollTallies>();

    private FakePollPublisher Published => Factory.Services.GetRequiredService<FakePollPublisher>();

    private WatchedPublisher Watched => Factory.Services.GetRequiredService<WatchedPublisher>();

    [SetUp]
    public void NewDeck() => _deck = $"deck-{Guid.NewGuid():N}";

    [Test]
    public async Task Pump_WhenManyChangesArrive_ShouldPublishAtMostTwiceASecond()
    {
        await ChangeAsync();
        var first = await PublishedAsync(1);
        await ChangeAsync(20);
        var duringTheFirstInterval = Published.PublishedFor(_deck).Count;
        Clock.Advance(_justBefore);
        var justBeforeItEnds = Published.PublishedFor(_deck).Count;
        Clock.Advance(PollTallyPump.Interval - _justBefore);
        var second = await PublishedAsync(2);
        await ChangeAsync(20);
        Clock.Advance(_justBefore);
        var duringTheSecondInterval = Published.PublishedFor(_deck).Count;
        Clock.Advance(PollTallyPump.Interval - _justBefore);
        var third = await PublishedAsync(3);

        first.ShouldBe([SLIDE]);
        duringTheFirstInterval.ShouldBe(1);
        justBeforeItEnds.ShouldBe(1);
        second.ShouldBe([SLIDE, SLIDE]);
        duringTheSecondInterval.ShouldBe(2);
        third.ShouldBe([SLIDE, SLIDE, SLIDE]);
    }

    // Trailing edge: a change inside the half second after a publish is published when it passes, and after a quiet half
    // second the next change is published at once.
    [Test]
    public async Task Pump_WhenTheLastChangeComesInsideTheInterval_ShouldStillPublishIt()
    {
        await ChangeAsync();
        await PublishedAsync(1);
        await ChangeAsync(1, slide: SLIDE + 1);
        Clock.Advance(PollTallyPump.Interval);
        var trailing = await PublishedAsync(2);
        Clock.Advance(PollTallyPump.Interval);
        var quiet = Published.PublishedFor(_deck).Count;
        Clock.Advance(PollTallyPump.Interval);
        await ChangeAsync();
        var afterQuiet = await PublishedAsync(3);

        trailing.ShouldBe([SLIDE, SLIDE + 1]);
        quiet.ShouldBe(2);
        afterQuiet.ShouldBe([SLIDE, SLIDE + 1, SLIDE]);
    }

    [Test]
    public async Task Pump_WhenAPublishThrows_ShouldKeepPumping()
    {
        Watched.FailNextFor(_deck, new InvalidOperationException("publish failed"));

        await ChangeAsync();
        await WithinAsync(Watched.PublishedAsync(_deck, 1));
        var failed = Published.PublishedFor(_deck).Count;
        await ChangeAsync();
        Clock.Advance(PollTallyPump.Interval);
        await WithinAsync(Watched.PublishedAsync(_deck, 2));
        var after = Published.PublishedFor(_deck);

        failed.ShouldBe(0);
        after.ShouldBe([SLIDE]);
    }

    // A cancellation from the publisher while the app is running is a failed publish like any other: the deck's window
    // must not be left behind with no timer, which would stop the deck being published again.
    [Test]
    public async Task Pump_WhenAPublishIsCancelled_ShouldKeepPumping()
    {
        Watched.FailNextFor(_deck, new OperationCanceledException("publish cancelled"));

        await ChangeAsync();
        await WithinAsync(Watched.PublishedAsync(_deck, 1));
        var failed = Published.PublishedFor(_deck).Count;
        await ChangeAsync();
        Clock.Advance(PollTallyPump.Interval);
        await WithinAsync(Watched.PublishedAsync(_deck, 2));
        Clock.Advance(PollTallyPump.Interval);
        Clock.Advance(PollTallyPump.Interval);
        await ChangeAsync();
        await WithinAsync(Watched.PublishedAsync(_deck, 3));
        var after = Published.PublishedFor(_deck);

        failed.ShouldBe(0);
        after.ShouldBe([SLIDE, SLIDE]);
    }

    // On start the pump loads the open rounds of sessions that have not ended, and leaves a round left open in an ended
    // session alone; then it subscribes.
    [Test]
    public async Task Start_WhenSessionsAreLiveOrEnded_ShouldLoadOnlyTheOpenRoundsOfLiveSessions()
    {
        var live = await SeedPollAsync();
        var liveRound = await Rows.RoundAsync(live.SlideId, live.SessionId);
        var ended = await SeedPollAsync();
        (await Client.DeleteAsync($"/api/decks/{ended.DeckId}/live")).EnsureSuccessStatusCode();
        var endedRound = await Rows.RoundAsync(ended.SlideId, ended.SessionId);
        var draft = await SeedPollAsync();
        var draftRound = await Rows.RoundAsync(draft.SlideId, draft.SessionId, PollPhase.Draft);
        var recording = new RecordingTallies(fail: false);

        await StartAndStopAsync(recording);

        recording.Read.ShouldContain(liveRound.Id.ToString());
        recording.Read.ShouldNotContain(endedRound.Id.ToString());
        recording.Read.ShouldNotContain(draftRound.Id.ToString());
        recording.Subscribed.ShouldBeTrue();
    }

    [Test]
    public async Task Start_WhenLoadingFails_ShouldStillStartAndSubscribe()
    {
        var live = await SeedPollAsync();
        await Rows.RoundAsync(live.SlideId, live.SessionId);
        var recording = new RecordingTallies(fail: true);

        await StartAndStopAsync(recording);

        recording.Read.ShouldNotBeEmpty();
        recording.Subscribed.ShouldBeTrue();
    }

    // Starts and stops a pump made from the app's services but over the given tally, each within 10 seconds.
    private async Task StartAndStopAsync(IPollTallies recording)
    {
        using var pump = ActivatorUtilities.CreateInstance<PollTallyPump>(Factory.Services, recording);
        await WithinAsync(pump.StartAsync(CancellationToken.None));
        await WithinAsync(pump.StopAsync(CancellationToken.None));
    }

    // Raises Changed for the test's deck, as the tally does after each answer.
    private async Task ChangeAsync(int times = 1, int slide = SLIDE)
    {
        var round = new PollRoundRef(_deck, PollKind.Slide, slide, Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), 3);
        for (var i = 0; i < times; i++)
        {
            await Tallies.ApplyAsync(round, [], [0]);
        }
    }

    // Waits, at most 10 seconds, until the deck has had count publish calls, and returns the slides of those recorded.
    private async Task<IReadOnlyList<int>> PublishedAsync(int count)
    {
        await WithinAsync(Watched.PublishedAsync(_deck, count));
        return Published.PublishedFor(_deck);
    }

    private static async Task WithinAsync(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))) != task)
        {
            throw new TimeoutException("The pump did not publish within 10 seconds.");
        }
        await task;
    }

    // The app's IPollPublisher here: it passes each publish to FakePollPublisher, which records it, except that the next
    // publish for a deck named by FailNextFor throws instead. PublishedAsync completes once a deck has had that many publish
    // calls, failed ones included, so a test waits for the pump without polling.
    private sealed class WatchedPublisher(FakePollPublisher published) : IPollPublisher
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, Exception> _failNext = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
        private readonly List<(string DeckId, int Count, TaskCompletionSource Done)> _waiters = [];

        public void FailNextFor(string deckId, Exception failure)
        {
            lock (_gate)
            {
                _failNext[deckId] = failure;
            }
        }

        public Task PublishedAsync(string deckId, int count)
        {
            lock (_gate)
            {
                if (_calls.GetValueOrDefault(deckId) >= count)
                {
                    return Task.CompletedTask;
                }
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((deckId, count, done));
                return done.Task;
            }
        }

        public async Task PublishAsync(string deckId, int slide)
        {
            Exception? failure;
            lock (_gate)
            {
                _failNext.Remove(deckId, out failure);
            }
            try
            {
                if (failure != null)
                {
                    throw failure;
                }
                await published.PublishAsync(deckId, slide);
            }
            finally
            {
                lock (_gate)
                {
                    var calls = _calls[deckId] = _calls.GetValueOrDefault(deckId) + 1;
                    foreach (var waiter in _waiters.Where(w => w.DeckId == deckId && w.Count <= calls).ToList())
                    {
                        waiter.Done.SetResult();
                        _waiters.Remove(waiter);
                    }
                }
            }
        }
    }

    // An IPollTallies for a start-up test: records each round read (and fails each read when asked) and whether Changed
    // was subscribed. Nothing else calls it.
    private sealed class RecordingTallies(bool fail) : IPollTallies
    {
        private readonly Lock _gate = new();
        private readonly List<string> _read = [];

        public IReadOnlyList<string> Read
        {
            get
            {
                lock (_gate)
                {
                    return [.. _read];
                }
            }
        }

        public bool Subscribed { get; private set; }

        public Task ApplyAsync(PollRoundRef round, IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks, bool hadText = false, bool hasText = false) => Task.CompletedTask;

        public Task<PollCounts> ReadAsync(string roundId, int optionCount)
        {
            lock (_gate)
            {
                _read.Add(roundId);
            }
            return fail ? throw new InvalidOperationException("load failed") : Task.FromResult(new PollCounts(0, new int[optionCount]));
        }

        public IDisposable Changed(Func<PollRoundRef, Task> handler)
        {
            Subscribed = true;
            return new Unsubscribe();
        }

        private sealed class Unsubscribe : IDisposable
        {
            public void Dispose()
            {
                // Nothing to remove: the recording tally never calls its handler.
            }
        }
    }
}
