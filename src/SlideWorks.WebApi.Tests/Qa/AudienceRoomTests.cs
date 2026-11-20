using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The real IAudienceRoom (Q1, Q3): the room for the slide on screen, worked out by QaRoomRules from the deck's settings
// row, the switch of the presenter's slide (SettablePresentations stands in for SW-4's registry), the session's open Q&A
// round and its revision in QaRevisions. Rows come from QaRowSeed; each test seeds a live deck of its own (QaSeed), with
// the presenter on the quote slide.
[TestFixture]
[CancelAfter(30000)]
public class AudienceRoomTests : QaTestFixture
{
    private QaRevisions Revisions => Factory.Services.GetRequiredService<QaRevisions>();

    [Test]
    public async Task ForDeckAsync_WhenDeckHasNoSettingsRow_ShouldUseTheDefaults()
    {
        var qa = await SeedQaAsync();
        var round = qa.RoundId.ToString();

        var room = await RoomAsync(qa.DeckId);

        room.ShouldBe(new QaRoom(round, QaRoomQuestions.Open, true, true, Revisions.Current(round)));
    }

    [Test]
    public async Task ForDeckAsync_WhenPresenterMovesToASwitchedOffSlide_ShouldBeOffHere()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SwitchAsync(qa.QaSlideId, new QaSlideSwitch(false, false));
        var onQuote = await RoomAsync(qa.DeckId);

        Presentations.Set(SettablePresentations.At(qa.DeckId, qa.QaSlide));
        var onQa = await RoomAsync(qa.DeckId);

        onQuote!.Questions.ShouldBe(QaRoomQuestions.Open);
        onQuote.Reactions.ShouldBeTrue();
        onQa.ShouldBe(onQuote with { Questions = QaRoomQuestions.OffHere, Reactions = false });
        onQa!.Round.ShouldBe(qa.RoundId.ToString());
    }

    [Test]
    public async Task ForDeckAsync_WhenPresenterIsPastTheLastSlide_ShouldUseTheLastSlidesSwitch()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SwitchAsync(qa.QaSlideId, new QaSlideSwitch(true, false));

        Presentations.Set(SettablePresentations.At(qa.DeckId, qa.QaSlide + 3));
        var room = await RoomAsync(qa.DeckId);

        room!.Reactions.ShouldBeFalse();
    }

    [Test]
    public async Task ForDeckAsync_WhenTheLastSlideIsDeletedWhileLive_ShouldUseTheNewLastSlidesSwitch()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SwitchAsync(qa.QuoteSlideId, new QaSlideSwitch(true, false));
        Presentations.Set(SettablePresentations.At(qa.DeckId, qa.QaSlide));

        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs))
        {
            var (isError, json) = await agent.CallToolAsync("delete_slide", new { deck = qa.DeckId, slide = qa.QaSlide });
            isError.ShouldBeFalse(json.ToString());
        }
        var room = await RoomAsync(qa.DeckId);

        room!.Reactions.ShouldBeFalse();
    }

    [Test]
    public async Task ForDeckAsync_WhenNoPresentation_ShouldFollowTheDeck()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SwitchAsync(qa.QuoteSlideId, new QaSlideSwitch(false, false));
        await QaRowSeed.SwitchAsync(qa.QaSlideId, new QaSlideSwitch(false, false));

        Presentations.Drop(qa.DeckId);
        var room = await RoomAsync(qa.DeckId);

        room!.Questions.ShouldBe(QaRoomQuestions.Open);
        room.Reactions.ShouldBeTrue();
    }

    [Test]
    public async Task ForDeckAsync_WhenDeckIsClosedAndSlideIsOff_ShouldBeClosed()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SettingsAsync(qa.DeckId, new QaSettings(QaQuestions.Closed, false, false, true, QaShowOn.QaSlide));
        await QaRowSeed.SwitchAsync(qa.QuoteSlideId, new QaSlideSwitch(false, true));

        var room = await RoomAsync(qa.DeckId);

        room.ShouldBe(new QaRoom(qa.RoundId.ToString(), QaRoomQuestions.Closed, false, true, Revisions.Current(qa.RoundId.ToString())));
    }

    [Test]
    public async Task ForDeckAsync_WhenTheRevisionMoves_ShouldCarryIt()
    {
        var qa = await SeedQaAsync();
        var before = (await RoomAsync(qa.DeckId))!.ListRevision;

        var bumped = Revisions.Bump(qa.RoundId.ToString());
        var after = (await RoomAsync(qa.DeckId))!.ListRevision;

        after.ShouldBe(bumped);
        after.ShouldBeGreaterThan(before);
    }

    [Test]
    public async Task ForDeckAsync_WhenNotLive_ShouldBeNull()
    {
        var qa = await SeedQaAsync();
        var live = await RoomAsync(qa.DeckId);

        var ended = await Client.DeleteAsync($"/api/decks/{qa.DeckId}/live");
        ended.EnsureSuccessStatusCode();
        var room = await RoomAsync(qa.DeckId);

        live.ShouldNotBeNull();
        room.ShouldBeNull();
        (await RoomAsync(Guid.NewGuid().ToString("N"))).ShouldBeNull();
    }

    private async Task<QaRoom?> RoomAsync(string deckId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAudienceRoom>().ForDeckAsync(deckId);
    }
}
