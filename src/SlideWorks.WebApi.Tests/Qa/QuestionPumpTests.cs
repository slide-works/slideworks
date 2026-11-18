using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The vote pump between tally changes and the Q&A publisher (Q5): FakePollTallies raises Changed as the real tally does after
// the answer writer commits a guest's upvotes, and FakeQuestionPublisher records each publish (or throws). The fixture's fake
// clock drives the half second, so nothing waits for it: a publish the clock allows runs on the thread pool or inside
// Clock.Advance, and a test waits for a publish it expects with a bounded wait on the publisher's call count. SW-6's pump
// also hears these changes; it publishes polls through a fake of its own. The app is shared by the fixture, so every test
// uses a deck of its own.
[TestFixture]
[CancelAfter(60000)]
public class QuestionPumpTests : QaTestFixture
{
    private static readonly TimeSpan _step = TimeSpan.FromMilliseconds(100);

    private string _deck = null!;

    private FakePollTallies Tallies => Factory.Services.GetRequiredService<FakePollTallies>();

    private FakeQuestionPublisher Publisher => Factory.Services.GetRequiredService<FakeQuestionPublisher>();

    private CountingPublisher Counted => Factory.Services.GetRequiredService<CountingPublisher>();

    protected override void ConfigureQaServices(IServiceCollection services)
    {
        FakePollTallies.AddTo(services);
        FakePollPublisher.AddTo(services);
        FakeQuestionPublisher.AddTo(services);
        services.AddSingleton<CountingPublisher>().AddSingleton<IQuestionPublisher>(provider => provider.GetRequiredService<CountingPublisher>());
    }

    [SetUp]
    public void NewDeck() => _deck = $"deck-{Guid.NewGuid():N}";

    // Ten upvote changes at 100 ms apart: one publish at once, one when the first half second passes, and nothing more until
    // the second ends, when the changes since go out together.
    [Test]
    public async Task Changed_WhenTenVotesArriveInASecond_ShouldPublishAtMostTwice()
    {
        await ChangeAsync(PollKind.Qa);
        await PublishedAsync(1);
        for (var vote = 2; vote <= 10; vote++)
        {
            Clock.Advance(_step);
            await ChangeAsync(PollKind.Qa);
        }
        await PublishedAsync(2);
        Clock.Advance(QuestionPump.Interval * 2 - _step * 9 - TimeSpan.FromMilliseconds(1));
        var withinTheSecond = Counted.CallsFor(_deck);
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        await PublishedAsync(3);

        withinTheSecond.ShouldBe(2);
        Counted.CallsFor(_deck).ShouldBe(3);
    }

    // SW-6's pump publishes a slide poll's changes. The positive control is a Q&A change on another deck, made after them:
    // once that is published the pump has run, and the slide poll's deck still has no publish.
    [Test]
    public async Task Changed_WhenRoundIsASlidePoll_ShouldPublishNothing()
    {
        var other = $"deck-{Guid.NewGuid():N}";

        await ChangeAsync(PollKind.Slide, times: 10);
        Clock.Advance(QuestionPump.Interval);
        await ChangeAsync(PollKind.Qa, deck: other);
        await WithinAsync(Counted.PublishedAsync(other, 1));
        var slidePoll = Counted.PublishedAsync(_deck, 1);
        var publishedLate = await Task.WhenAny(slidePoll, Task.Delay(TimeSpan.FromMilliseconds(500))) == slidePoll;

        publishedLate.ShouldBeFalse();
        Counted.CallsFor(_deck).ShouldBe(0);
        Counted.CallsFor(other).ShouldBe(1);
    }

    // The writer awaits the tally's Changed handlers after its commit: a publish that throws must not reach it, and the deck
    // is still published after the failure.
    [Test]
    public async Task Changed_WhenPublisherThrows_ShouldNotFailTheVote()
    {
        Publisher.FailWith(new InvalidOperationException("publish failed"));
        try
        {
            await Should.NotThrowAsync(() => ChangeAsync(PollKind.Qa));
            await PublishedAsync(1);
        }
        finally
        {
            Publisher.FailWith(null);
        }
        await ChangeAsync(PollKind.Qa);
        Clock.Advance(QuestionPump.Interval);
        await PublishedAsync(2);

        Counted.FailedFor(_deck).ShouldBe(1);
        Counted.CallsFor(_deck).ShouldBe(2);
    }

    // One pump for the app, the same from every scope: a second instance would publish each change twice.
    [Test]
    public void Pump_WhenResolved_ShouldBeASingleton()
    {
        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();

        var fromRoot = Factory.Services.GetServices<IHostedService>().OfType<QuestionPump>().ShouldHaveSingleItem();
        var fromFirst = first.ServiceProvider.GetServices<IHostedService>().OfType<QuestionPump>().ShouldHaveSingleItem();
        var fromSecond = second.ServiceProvider.GetServices<IHostedService>().OfType<QuestionPump>().ShouldHaveSingleItem();

        fromFirst.ShouldBeSameAs(fromRoot);
        fromSecond.ShouldBeSameAs(fromRoot);
    }

    // Raises Changed for a round of the given kind on the test's deck (or another), as the tally does after each answer.
    private async Task ChangeAsync(PollKind kind, int times = 1, string? deck = null)
    {
        var round = kind == PollKind.Qa
            ? new PollRoundRef(deck ?? _deck, kind, null, null, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), 3)
            : new PollRoundRef(deck ?? _deck, kind, 2, Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), 3);
        for (var i = 0; i < times; i++)
        {
            await Tallies.ApplyAsync(round, [], [0]);
        }
    }

    // Waits, at most 10 seconds, until the test's deck has had count publish calls, failed ones included.
    private Task PublishedAsync(int count) => WithinAsync(Counted.PublishedAsync(_deck, count));

    private static async Task WithinAsync(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))) != task)
        {
            throw new TimeoutException("The pump did not publish within 10 seconds.");
        }
        await task;
    }

    // The app's IQuestionPublisher here: it passes each publish to FakeQuestionPublisher (which records it, or throws what
    // FailWith set) and counts the calls per deck, failed ones too. PublishedAsync completes once a deck has had that many
    // calls, so a test waits for the pump without polling.
    private sealed class CountingPublisher(FakeQuestionPublisher fake) : IQuestionPublisher
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _failed = new(StringComparer.Ordinal);
        private readonly List<(string DeckId, int Count, TaskCompletionSource Done)> _waiters = [];

        public int CallsFor(string deckId)
        {
            lock (_gate)
            {
                return _calls.GetValueOrDefault(deckId);
            }
        }

        public int FailedFor(string deckId)
        {
            lock (_gate)
            {
                return _failed.GetValueOrDefault(deckId);
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

        public async Task PublishAsync(string deckId)
        {
            var failed = false;
            try
            {
                await fake.PublishAsync(deckId);
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                lock (_gate)
                {
                    var calls = _calls[deckId] = _calls.GetValueOrDefault(deckId) + 1;
                    if (failed)
                    {
                        _failed[deckId] = _failed.GetValueOrDefault(deckId) + 1;
                    }
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
