using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The arrival rule (V4, D2, D12) through the presentation observers: the presenter is moved as SW-4's registry moves it
// (record the state, tell every observer), and FakePolls records which IPolls actions arrival called, in order. Rounds and
// settings come from PollRowSeed. The app, and so FakePolls, is shared by the fixture: each test makes its own deck, and the
// refusal is reset after each.
[TestFixture]
public class PollArrivalTests : PollTestFixture
{
    private FakePolls Polls => Factory.Services.GetRequiredService<FakePolls>();

    protected override void ConfigurePollServices(IServiceCollection services) => FakePolls.AddTo(services);

    [TearDown]
    public void AcceptAgain() => Polls.Reset();

    [Test]
    public async Task Presenter_WhenArrivingOnAnOnArrivalPoll_ShouldOpenIt()
    {
        var poll = await SeedAsync();

        await MoveAsync(poll.DeckId, poll.Slide);

        Polls.CallsFor(poll.DeckId).ShouldBe([$"Open {poll.Slide}"]);
    }

    [Test]
    public async Task Presenter_WhenLeavingAnOpenPoll_ShouldCloseIt()
    {
        var poll = await SeedAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);

        await MoveAsync(poll.DeckId, poll.OtherSlide);

        Polls.CallsFor(poll.DeckId).ShouldBe([$"Close {poll.Slide}"]);
    }

    [Test]
    public async Task Presenter_WhenLeavingAnOpenPollForAnOnArrivalPoll_ShouldCloseThenOpen()
    {
        var poll = await SeedAsync();
        var next = await AddPollAsync(poll);
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);

        await MoveAsync(poll.DeckId, next);

        Polls.CallsFor(poll.DeckId).ShouldBe([$"Close {poll.Slide}", $"Open {next}"]);
    }

    // A poll is open whenever the presenter is on it (changed 27 Nov 2026: returning used to leave it closed, and phones
    // showed a dead card).
    [Test]
    public async Task Presenter_WhenReturningToAClosedPoll_ShouldReopenIt()
    {
        var poll = await SeedAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(round.Id, joinedAtClose: 0);

        await MoveAsync(poll.DeckId, poll.OtherSlide);
        await MoveAsync(poll.DeckId, poll.Slide);

        Polls.CallsFor(poll.DeckId).ShouldBe([$"Reopen {poll.Slide}"]);
    }

    [Test]
    public async Task Presenter_WhenStayingOnAPollClosedThere_ShouldCallNothing()
    {
        var poll = await SeedAsync();
        await MoveAsync(poll.DeckId, poll.Slide);
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(round.Id, joinedAtClose: 0);
        Polls.Reset();
        var before = Polls.CallsFor(poll.DeckId).Count;

        await MoveAsync(poll.DeckId, poll.Slide, step: 0);

        Polls.CallsFor(poll.DeckId).Count.ShouldBe(before);
    }

    [Test]
    public async Task Presenter_WhenReturningToAClosedManualPoll_ShouldCallNothing()
    {
        var poll = await SeedAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Opens = PollOpens.Manual });
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(round.Id, joinedAtClose: 0);

        await MoveAsync(poll.DeckId, poll.OtherSlide);
        await MoveAsync(poll.DeckId, poll.Slide);

        Polls.CallsFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task Presenter_WhenArrivingOnAManualPoll_ShouldCallNothing()
    {
        var poll = await SeedAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Opens = PollOpens.Manual });

        await MoveAsync(poll.DeckId, poll.Slide);

        Polls.CallsFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task Presenter_WhenStateRepeatsForTheSameSlide_ShouldCallNothing()
    {
        var poll = await SeedAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);

        await MoveAsync(poll.DeckId, poll.Slide);
        await MoveAsync(poll.DeckId, poll.Slide, step: 0);

        Polls.CallsFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task Presenter_WhenDeckIsNotLive_ShouldCallNothing()
    {
        var poll = await SeedAsync();
        var ended = await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live");
        var beforeMove = Polls.CallsFor(poll.DeckId);

        await MoveAsync(poll.DeckId, poll.Slide);

        ended.IsSuccessStatusCode.ShouldBeTrue();
        Polls.CallsFor(poll.DeckId).ShouldBe(beforeMove);
    }

    // Arrival leaves a draft alone: it is discarded only by Open or New round on another slide, or by the end of the session.
    [Test]
    public async Task Presenter_WhenLeavingADraftForASlideThatIsNotAPoll_ShouldCallNothing()
    {
        var poll = await SeedAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Opens = PollOpens.Manual });
        await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft);

        await MoveAsync(poll.DeckId, poll.OtherSlide);
        await MoveAsync(poll.DeckId, poll.Slide);

        Polls.CallsFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task Presenter_WhenLeavingADraftForAnOnArrivalPoll_ShouldOnlyOpenTheNewPoll()
    {
        var poll = await SeedAsync();
        var next = await AddPollAsync(poll);
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Opens = PollOpens.Manual });
        await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft);

        await MoveAsync(poll.DeckId, next);

        Polls.CallsFor(poll.DeckId).ShouldBe([$"Open {next}"]);
    }

    [Test]
    public async Task Presenter_WhenOpeningIsRefused_ShouldLetTheMoveLand()
    {
        var poll = await SeedAsync();
        Polls.RefuseWith(PollRefusals.ANOTHER_POLL_OPEN);

        var move = () => MoveAsync(poll.DeckId, poll.Slide);

        await move.ShouldNotThrowAsync();
        Polls.CallsFor(poll.DeckId).ShouldBeEmpty();
    }

    // A deck with a poll on slide 2, live, FakePolls answering for its slides; the presenter is put on slide 1 without
    // telling the observers, so each test's own moves are the only arrivals.
    private async Task<SeededPoll> SeedAsync()
    {
        var poll = await SeedPollAsync();
        Presentations.Set(SettablePresentations.At(poll.DeckId, poll.OtherSlide));
        Polls.Set(poll.DeckId, poll.Slide, View(poll.Slide));
        return poll;
    }

    // Appends a second poll slide to the seeded deck and has FakePolls answer for it. Returns its number.
    private async Task<int> AddPollAsync(SeededPoll poll)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var added = await DeckSeed.AddSlideAsync(agent, poll.DeckId, "poll", JsonSerializer.Serialize(new { question = "Which day do we ship?", options = new[] { "Tuesday", "Friday" } }));
        var slide = added.GetProperty("slide").GetInt32();
        Polls.Set(poll.DeckId, slide, View(slide));
        return slide;
    }

    private static PollControlView View(int slide) => new(
        1, slide, null, 1, PollPhase.Draft, PollSettings.Default, PollSeed.QUESTION, PollSeed.OPTIONS,
        0, 0, [0, 0, 0], true, false, false, []);
}
