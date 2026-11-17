using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The one poll publisher (V5) against FakePolls and FakePollTallies. Each test reads what reached the audience bus
// (IAudienceEvents) through a subscription of its own, with each event's type and Target, and ends the read at a marker it
// publishes after the publisher returns: the bus keeps a subscriber's events in order, so nothing the publisher sent can
// come after it. Which hub group an event reaches is AudienceEventsRelay's, and is not tested here. The app (and so each
// fake) is shared by the fixture, so every test uses a deck and a round of its own.
[TestFixture]
[CancelAfter(30000)]
public class PollPublisherTests : ApiTestFixture
{
    private const int SLIDE = 4;
    private static readonly int[] _counts = [5, 9, 4, 3];

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakePolls.AddTo(services);
        FakePollTallies.AddTo(services);
    };

    private string _deck = null!;

    private string _round = null!;

    private FakePolls Polls => Factory.Services.GetRequiredService<FakePolls>();

    private FakePollTallies Tallies => Factory.Services.GetRequiredService<FakePollTallies>();

    [SetUp]
    public void NewDeckAndRound()
    {
        _deck = $"deck-{Guid.NewGuid():N}";
        _round = Guid.NewGuid().ToString();
    }

    [Test]
    public async Task Publish_WhenResultsAreAfterCloseAndOpen_ShouldPublishANullCountTallyAndAControlWithCounts()
    {
        Polls.Set(_deck, SLIDE, View(PollPhase.Open, PollResults.AfterClose));
        Tallies.Set(_round, new PollCounts(21, _counts));

        var published = await PublishAsync();

        published.Count.ShouldBe(2);
        var control = published[0].ShouldBeOfType<PollControl>();
        control.Control.Answered.ShouldBe(21);
        control.Control.Counts.ShouldBe(_counts);
        var tally = published[1].ShouldBeOfType<PollTally>();
        tally.Target.ShouldBe(PollTallyTarget.Audience);
        tally.Round.ShouldBe(_round);
        tally.Answered.ShouldBe(21);
        tally.Counts.ShouldBeNull();
    }

    [Test]
    public async Task Publish_WhenResultsAreAfterAnswerAndOpen_ShouldAlsoPublishATallyTargetedAtResults()
    {
        Polls.Set(_deck, SLIDE, View(PollPhase.Open, PollResults.AfterAnswer));
        Tallies.Set(_round, new PollCounts(21, _counts));

        var published = await PublishAsync();

        published.Count.ShouldBe(3);
        published[0].ShouldBeOfType<PollControl>().Control.Counts.ShouldBe(_counts);
        var audience = published[1].ShouldBeOfType<PollTally>();
        audience.Target.ShouldBe(PollTallyTarget.Audience);
        audience.Counts.ShouldBeNull();
        audience.Answered.ShouldBe(21);
        var results = published[2].ShouldBeOfType<PollTally>();
        results.Target.ShouldBe(PollTallyTarget.Results);
        results.Round.ShouldBe(_round);
        results.Answered.ShouldBe(21);
        results.Counts.ShouldBe(_counts);
    }

    [Test]
    public async Task Publish_WhenClosed_ShouldPublishAnAudienceTallyWithCounts()
    {
        Polls.Set(_deck, SLIDE, View(PollPhase.Closed, PollResults.AfterAnswer) with { Answered = 38, Counts = _counts });
        Tallies.Set(_round, new PollCounts(20, [1, 2, 3, 4]));

        var published = await PublishAsync();

        published.Count.ShouldBe(2);
        var control = published[0].ShouldBeOfType<PollControl>();
        control.Control.Answered.ShouldBe(38);
        control.Control.Counts.ShouldBe(_counts);
        var tally = published[1].ShouldBeOfType<PollTally>();
        tally.Target.ShouldBe(PollTallyTarget.Audience);
        tally.Answered.ShouldBe(38);
        tally.Counts.ShouldBe(_counts);
    }

    [Test]
    public async Task Publish_WhenOpenAndRevealed_ShouldPublishAnAudienceTallyWithCounts()
    {
        Polls.Set(_deck, SLIDE, View(PollPhase.Open, PollResults.AfterClose) with { Revealed = true });
        Tallies.Set(_round, new PollCounts(21, _counts));

        var published = await PublishAsync();

        published.Count.ShouldBe(2);
        var tally = published[1].ShouldBeOfType<PollTally>();
        tally.Target.ShouldBe(PollTallyTarget.Audience);
        tally.Counts.ShouldBe(_counts);
    }

    [Test]
    public async Task Publish_WhenDraft_ShouldPublishOnlyTheControl()
    {
        Polls.Set(_deck, SLIDE, View(PollPhase.Draft, PollResults.Live) with { Round = null, Answered = 0, Counts = [0, 0, 0, 0] });

        var published = await PublishAsync();

        published.Count.ShouldBe(1);
        published[0].ShouldBeOfType<PollControl>().Control.Phase.ShouldBe(PollPhase.Draft);
    }

    [Test]
    public async Task Publish_WhenDraftHasARoundId_ShouldPublishOnlyTheControl()
    {
        Polls.Set(_deck, SLIDE, View(PollPhase.Draft, PollResults.Live) with { Answered = 0, Counts = [0, 0, 0, 0] });

        var published = await PublishAsync();

        published.Count.ShouldBe(1);
        published[0].ShouldBeOfType<PollControl>().Control.Phase.ShouldBe(PollPhase.Draft);
    }

    [Test]
    public async Task Publish_WhenTwoOverlapForOneDeck_ShouldSendEachWholeAndEndOnTheLaterRead()
    {
        int[] final = [12, 25, 14, 6];
        Polls.Set(_deck, SLIDE, View(PollPhase.Open, PollResults.Live));
        Tallies.Set(_round, new PollCounts(20, _counts));
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();
        var publisher = Factory.Services.GetRequiredService<IPollPublisher>();
        var received = Channel.CreateUnbounded<AudienceEvent>();
        using var subscription = bus.Subscribe(_deck, e => received.Writer.WriteAsync(e).AsTask());
        var readStarted = Tallies.BlockNextRead();

        var pump = publisher.PublishAsync(_deck, SLIDE);
        await readStarted;
        Polls.Set(_deck, SLIDE, View(PollPhase.Closed, PollResults.Live) with { Answered = 38, Counts = final });
        var close = publisher.PublishAsync(_deck, SLIDE);
        await Task.WhenAny(close, Task.Delay(TimeSpan.FromMilliseconds(200)));
        Tallies.ReleaseRead();
        await Task.WhenAll(pump, close);
        var published = await DrainAsync(bus, received);

        published.Count.ShouldBe(4);
        var firstControl = published[0].ShouldBeOfType<PollControl>();
        firstControl.Control.Phase.ShouldBe(PollPhase.Open);
        published[1].ShouldBeOfType<PollTally>().Answered.ShouldBe(20);
        var secondControl = published[2].ShouldBeOfType<PollControl>();
        secondControl.Control.Phase.ShouldBe(PollPhase.Closed);
        secondControl.Control.Seq.ShouldBeGreaterThan(firstControl.Control.Seq);
        var last = published[3].ShouldBeOfType<PollTally>();
        last.Answered.ShouldBe(38);
        last.Counts.ShouldBe(final);
    }

    [Test]
    public async Task Publish_WhenTheSlideIsNotAPoll_ShouldPublishNothing()
    {
        var published = await PublishAsync();

        published.ShouldBeEmpty();
    }

    [Test]
    public async Task Publish_WhenCalledTwice_ShouldRaiseSeq()
    {
        Polls.Set(_deck, SLIDE, View(PollPhase.Open, PollResults.Live));

        var first = await PublishAsync();
        var second = await PublishAsync();

        var firstSeq = first[0].ShouldBeOfType<PollControl>().Control.Seq;
        second[0].ShouldBeOfType<PollControl>().Control.Seq.ShouldBeGreaterThan(firstSeq);
    }

    // Publishes the poll on SLIDE of the test's deck and returns every audience event the publisher sent for the deck, in order.
    private async Task<IReadOnlyList<AudienceEvent>> PublishAsync()
    {
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();
        var received = Channel.CreateUnbounded<AudienceEvent>();
        using var subscription = bus.Subscribe(_deck, e => received.Writer.WriteAsync(e).AsTask());
        await Factory.Services.GetRequiredService<IPollPublisher>().PublishAsync(_deck, SLIDE);
        return await DrainAsync(bus, received);
    }

    // Publishes a marker and returns every event received before it.
    private async Task<IReadOnlyList<AudienceEvent>> DrainAsync(IAudienceEvents bus, Channel<AudienceEvent> received)
    {
        var marker = new AudienceEnded(_deck);
        await bus.PublishAsync(marker);
        var events = new List<AudienceEvent>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var next = await received.Reader.ReadAsync(timeout.Token); !ReferenceEquals(next, marker); next = await received.Reader.ReadAsync(timeout.Token))
        {
            events.Add(next);
        }
        return events;
    }

    private PollControlView View(PollPhase phase, PollResults results)
        => new(
            1,
            SLIDE,
            _round,
            1,
            phase,
            PollSettings.Default with { Results = results },
            "How big was your last merge request?",
            ["Under 50 lines", "50 to 200 lines", "200 to 1,000 lines", "Over 1,000 lines"],
            0,
            41,
            [0, 0, 0, 0],
            PollVisibility.RoomSees(results, false, phase == PollPhase.Closed),
            false,
            false,
            []);
}
