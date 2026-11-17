using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The pump between tally changes and the publisher (V5): FakePollTallies raises Changed as the real tally does after an
// answer, and FakePollPublisher records each publish. The fixture's fake clock drives the half second, so nothing here
// sleeps: a publish that the clock allows runs on the thread pool, and the tests wait for it with a bounded wait. The app
// is shared by the fixture, so every test uses a deck of its own.
[TestFixture]
[CancelAfter(60000)]
public class PollPumpTests : ApiTestFixture
{
    private const int SLIDE = 3;

    private static readonly TimeSpan _justBefore = PollTallyPump.Interval - TimeSpan.FromMilliseconds(1);

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakePollTallies.AddTo(services);
        FakePollPublisher.AddTo(services);
        services.AddSingleton<WatchedPublisher>().AddSingleton<IPollPublisher>(provider => provider.GetRequiredService<WatchedPublisher>());
    };

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
        Watched.FailNextFor(_deck);

        await ChangeAsync();
        await WithinAsync(Watched.PublishedAsync(_deck, 1));
        var failed = Published.PublishedFor(_deck).Count;
        await ChangeAsync();
        Clock.Advance(PollTallyPump.Interval);
        var after = await PublishedAsync(1);

        failed.ShouldBe(0);
        after.ShouldBe([SLIDE]);
    }

    // Raises Changed for the test's deck, as the tally does after each answer.
    private async Task ChangeAsync(int times = 1, int slide = SLIDE)
    {
        var round = new PollRoundRef(_deck, slide, Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), 3);
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
        private readonly HashSet<string> _failNext = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
        private readonly List<(string DeckId, int Count, TaskCompletionSource Done)> _waiters = [];

        public void FailNextFor(string deckId)
        {
            lock (_gate)
            {
                _failNext.Add(deckId);
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
            bool fail;
            lock (_gate)
            {
                fail = _failNext.Remove(deckId);
            }
            try
            {
                if (fail)
                {
                    throw new InvalidOperationException("publish failed");
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
}
