using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The one Q&A publisher (Q5) against FakeQuestionModeration, and the list revisions it stamps (QaRevisions over QaSeq). Each
// publisher test seeds a live deck of its own (PollSeed), sets the presenter's view the fake answers with, and reads what
// reached the audience bus (IAudienceEvents) through a subscription of its own, ending the read at a marker it publishes
// after the publisher returns: the bus keeps a subscriber's events in order, so nothing the publisher sent can come after
// it. Only the Q&A events are kept, since the live deck's own audience state may also be on the bus. Which hub group an
// event reaches is AudienceEventsRelay's, and is not tested here.
[TestFixture]
[CancelAfter(30000)]
public class QuestionPublisherTests : PollTestFixture
{
    protected override void ConfigurePollServices(IServiceCollection services) => FakeQuestionModeration.AddTo(services);

    private FakeQuestionModeration Moderation => Factory.Services.GetRequiredService<FakeQuestionModeration>();

    private QaRevisions Revisions => Factory.Services.GetRequiredService<QaRevisions>();

    private QaSeq Seq => Factory.Services.GetRequiredService<QaSeq>();

    [Test]
    public async Task PublishAsync_WhenAQuestionIsHidden_ShouldLeaveItOutOfTheTally()
    {
        var deck = (await SeedPollAsync()).DeckId;
        var round = NewRound();
        Moderation.Set(deck, Live(round, [Question(1, QuestionState.Open, 4), Question(2, QuestionState.Open, 7) with { Hidden = true }, Question(3, QuestionState.Answered, 2)]));

        var published = await PublishAsync(deck);

        var tally = published.OfType<QuestionTally>().ShouldHaveSingleItem();
        tally.Votes.ShouldBe([new QuestionVotes(1, 4), new QuestionVotes(3, 2)]);
        published.OfType<QuestionControl>().ShouldHaveSingleItem().Control.Questions.Select(q => q.Number).ShouldBe([1, 2, 3]);
    }

    [Test]
    public async Task PublishAsync_WhenAQuestionIsWaiting_ShouldLeaveItOutOfTheTally()
    {
        var deck = (await SeedPollAsync()).DeckId;
        var round = NewRound();
        Moderation.Set(deck, Live(round, [Question(1, QuestionState.Waiting, 1), Question(2, QuestionState.Open, 5), Question(3, QuestionState.Waiting, 0) with { Hidden = true }]));

        var published = await PublishAsync(deck);

        var tally = published.OfType<QuestionTally>().ShouldHaveSingleItem();
        tally.Votes.ShouldBe([new QuestionVotes(2, 5)]);
    }

    [Test]
    public async Task PublishAsync_WhenCalled_ShouldPublishOneControlAndOneTally()
    {
        var deck = (await SeedPollAsync()).DeckId;
        var round = NewRound();
        var revision = Revisions.Bump(round);
        var view = Live(round, [Question(2, QuestionState.Open, 9), Question(1, QuestionState.Open, 14)]);
        Moderation.Set(deck, view);
        var before = Seq.Next();

        var published = await PublishAsync(deck);

        published.Count.ShouldBe(2);
        var control = published[0].ShouldBeOfType<QuestionControl>();
        control.DeckId.ShouldBe(deck);
        control.Control.Seq.ShouldBeGreaterThan(before);
        control.Control.ShouldBe(view with { Seq = control.Control.Seq });
        var tally = published[1].ShouldBeOfType<QuestionTally>();
        tally.DeckId.ShouldBe(deck);
        tally.Round.ShouldBe(round);
        tally.ListRevision.ShouldBe(revision);
        tally.Votes.ShouldBe([new QuestionVotes(1, 14), new QuestionVotes(2, 9)]);
    }

    [Test]
    public async Task PublishAsync_WhenNotLive_ShouldPublishTheControlOnly()
    {
        var deck = (await SeedPollAsync()).DeckId;
        Moderation.Set(deck, NotLive());

        var published = await PublishAsync(deck);

        published.ShouldHaveSingleItem().ShouldBeOfType<QuestionControl>().Control.Live.ShouldBeFalse();
    }

    [Test]
    public async Task PublishAsync_WhenLiveWithQuestionsOff_ShouldPublishTheControlOnly()
    {
        var deck = (await SeedPollAsync()).DeckId;
        Moderation.Set(deck, Live(NewRound(), []) with { Round = null, Settings = QaSettings.Default with { Questions = QaQuestions.Off } });

        var published = await PublishAsync(deck);

        published.ShouldHaveSingleItem().ShouldBeOfType<QuestionControl>().Control.Round.ShouldBeNull();
    }

    [Test]
    public async Task PublishAsync_WhenQuestionsOffButTheViewHasARound_ShouldPublishTheControlOnly()
    {
        var deck = (await SeedPollAsync()).DeckId;
        var round = NewRound();
        Moderation.Set(deck, Live(round, [Question(1, QuestionState.Open, 3)]) with { Settings = QaSettings.Default with { Questions = QaQuestions.Off } });

        var published = await PublishAsync(deck);

        published.ShouldHaveSingleItem().ShouldBeOfType<QuestionControl>().Control.Round.ShouldBe(round);
    }

    [Test]
    public async Task PublishAsync_WhenCalledTwice_ShouldRaiseSeqAndKeepTheRevisionUntilBumped()
    {
        var deck = (await SeedPollAsync()).DeckId;
        var round = NewRound();
        Moderation.Set(deck, Live(round, [Question(1, QuestionState.Open, 1)]));

        var first = await PublishAsync(deck);
        var second = await PublishAsync(deck);
        var bumped = Revisions.Bump(round);
        var third = await PublishAsync(deck);

        var firstSeq = first.OfType<QuestionControl>().Single().Control.Seq;
        second.OfType<QuestionControl>().Single().Control.Seq.ShouldBeGreaterThan(firstSeq);
        var firstRevision = first.OfType<QuestionTally>().Single().ListRevision;
        second.OfType<QuestionTally>().Single().ListRevision.ShouldBe(firstRevision);
        bumped.ShouldBeGreaterThan(firstRevision);
        third.OfType<QuestionTally>().Single().ListRevision.ShouldBe(bumped);
    }

    [Test]
    public void Bump_WhenCalledTwice_ShouldOnlyIncrease()
    {
        var round = NewRound();
        var start = Revisions.Current(round);

        var first = Revisions.Bump(round);
        var second = Revisions.Bump(round);

        first.ShouldBeGreaterThan(start);
        second.ShouldBeGreaterThan(first);
        Revisions.Current(round).ShouldBe(second);
    }

    [Test]
    public void Current_WhenRoundIsNew_ShouldStartFromTheSequence()
    {
        var before = Seq.Next();

        var current = Revisions.Current(NewRound());

        current.ShouldBeGreaterThan(before);
        Seq.Next().ShouldBeGreaterThan(current);
    }

    [Test]
    public void Current_WhenReadTwice_ShouldNotMove()
    {
        var round = NewRound();

        var first = Revisions.Current(round);

        Revisions.Current(round).ShouldBe(first);
    }

    [Test]
    public void Next_WhenTheClockMovesForward_ShouldBeAtLeastTheClock()
    {
        var clock = new FakeTimeProvider(Clock.GetUtcNow());
        var start = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var seq = new QaSeq(clock);
        var first = seq.Next();

        clock.Advance(TimeSpan.FromMinutes(5));
        var next = seq.Next();

        first.ShouldBeGreaterThanOrEqualTo(start);
        next.ShouldBeGreaterThanOrEqualTo(clock.GetUtcNow().ToUnixTimeMilliseconds());
        next.ShouldBeGreaterThan(first + 1);
    }

    // Publishes the deck and returns every Q&A event the publisher sent for it, in order.
    private async Task<IReadOnlyList<AudienceEvent>> PublishAsync(string deck)
    {
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();
        var received = Channel.CreateUnbounded<AudienceEvent>();
        using var subscription = bus.Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());
        await Factory.Services.GetRequiredService<IQuestionPublisher>().PublishAsync(deck);
        var marker = new AudienceEnded(deck);
        await bus.PublishAsync(marker);
        var events = new List<AudienceEvent>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var next = await received.Reader.ReadAsync(timeout.Token); !ReferenceEquals(next, marker); next = await received.Reader.ReadAsync(timeout.Token))
        {
            if (next is QuestionControl or QuestionTally)
            {
                events.Add(next);
            }
        }
        return events;
    }

    private static string NewRound() => Guid.NewGuid().ToString();

    private static QaQuestion Question(int number, QuestionState state, int votes)
        => new(number, $"Question {number}?", null, "now", state, false, false, votes);

    private static QaControlView Live(string round, IReadOnlyList<QaQuestion> questions)
        => new(
            1,
            true,
            round,
            QaSettings.Default,
            new QaSlideView(2, true, true),
            [],
            new QaCounts(questions.Count(q => !q.Hidden && q.State == QuestionState.Open), questions.Count(q => !q.Hidden && q.State == QuestionState.Waiting), questions.Count(q => !q.Hidden && q.State == QuestionState.Answered), questions.Count(q => q.Hidden)),
            null,
            questions,
            ReactionCounts.Zero);

    private static QaControlView NotLive()
        => new(1, false, null, QaSettings.Default, null, [], new QaCounts(0, 0, 0, 0), null, [], ReactionCounts.Zero);
}
