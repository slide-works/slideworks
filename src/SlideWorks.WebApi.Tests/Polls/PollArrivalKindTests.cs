using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The arrival rule (V4, D2) with a Q&A round open in the same session (SW-9 Q2): arrival reads only the session's slide
// round, so leaving an open slide poll still closes it while the session's Q&A round stays open. Same set-up as
// PollArrivalTests: the presenter moves as SW-4's registry moves it, and FakePolls records which IPolls actions arrival
// called. Rounds come from PollRowSeed.
[TestFixture]
public class PollArrivalKindTests : PollTestFixture
{
    private FakePolls Polls => Factory.Services.GetRequiredService<FakePolls>();

    protected override void ConfigurePollServices(IServiceCollection services) => FakePolls.AddTo(services);

    [TearDown]
    public void AcceptAgain() => Polls.Reset();

    [Test]
    public async Task Presenter_WhenLeavingAnOpenPollBesideAnOpenQaRound_ShouldCloseIt()
    {
        var poll = await SeedAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.QaRoundAsync(poll.DeckId, poll.SessionId);

        await MoveAsync(poll.DeckId, poll.OtherSlide);

        Polls.CallsFor(poll.DeckId).ShouldBe([$"Close {poll.Slide}"]);
    }

    // A deck with a poll slide, live, the presenter on the other slide, and FakePolls answering for the poll slide.
    private async Task<SeededPoll> SeedAsync()
    {
        var poll = await SeedPollAsync();
        Presentations.Set(SettablePresentations.At(poll.DeckId, poll.OtherSlide));
        Polls.Set(poll.DeckId, poll.Slide, View(poll.Slide));
        return poll;
    }

    private static PollControlView View(int slide) => new(
        1, slide, null, 1, PollPhase.Draft, PollSettings.Default, PollSeed.QUESTION, PollSeed.OPTIONS,
        0, 0, [0, 0, 0], true, false, false, []);
}
