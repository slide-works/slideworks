using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// PDF export end to end, every piece real: Sam presents a deck with a poll live, a guest answers it, the poll closes and
// the talk ends; the real print page then draws that poll's result, and the deck page's Download PDF saves the real
// engine's file. Each test runs the talk on a deck of its own, since the server caches a deck's PDF. The poll is a
// choice poll: text and cloud answers do not reach the server (SW-878).
public class PdfExportJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL = 2;
    private const int PICKED = 1;

    [Test]
    public async Task PrintPage_WhenAPollRanInTheLastTalk_ShouldDrawItsResult()
    {
        var deck = await RunTalkAsync();
        var token = AcceptanceApp.Services.GetRequiredService<IRenderTokens>().Issue(deck.Id, RenderPurpose.Print);
        var print = new PrintPage(await Page.Context.NewPageAsync());

        await print.GotoAsync(deck.Id, token);

        await Expect(print.Root).ToHaveAttributeAsync("data-ready", "true");
        await Expect(print.Slides).ToHaveCountAsync(SLIDES);
        var poll = print.Poll(POLL);
        await Expect(poll.Question).ToHaveTextAsync(QUESTION);
        await Expect(poll.Percent(PICKED)).ToHaveTextAsync("100%");
    }

    [Test]
    public async Task DownloadPdf_WhenATalkWithAPollHasEnded_ShouldSaveOnePagePerSlide()
    {
        var deck = await RunTalkAsync();
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deck.Id);
        await Expect(deckPage.SlideCount).ToBeVisibleAsync();

        var download = await deckPage.DownloadPdfAsync();

        download.SuggestedFilename.ShouldBe($"{deck.Slug}.pdf");
        var path = await download.PathAsync();
        var pdf = await File.ReadAllBytesAsync(path.ShouldNotBeNull());
        PdfPages.IsPdf(pdf).ShouldBeTrue();
        PdfPages.Count(pdf).ShouldBe(SLIDES);
    }

    private const string QUESTION = "Which release train do we board?";

    // Sam's deck (a join slide, a choice poll, a quote) goes in over MCP. Sam goes live in the presenter window, one guest
    // joins and answers the poll when the presenter reaches it, the poll closes and the talk ends.
    private async Task<Deck> RunTalkAsync()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Export {Guid.NewGuid():N}" });
        var id = created.GetProperty("deck").GetString().ShouldNotBeNull();
        var slug = new Uri(created.GetProperty("url").GetString().ShouldNotBeNull()).Segments[^1];
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = id, ["layout"] = "join", ["slots"] = new { } });
        await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = id,
            ["layout"] = "poll",
            ["slots"] = new { question = QUESTION, options = new[] { "The morning train", "The noon train", "The night train" } },
        });
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = id, ["layout"] = "quote", ["slots"] = new { quote = "Ship small.", attribution = "Sam Rivera" } });

        await SignIn.AsAsync(Page, TestUser.Sam);
        var view = await PollJourney.ReadAsync(Page, $"/api/decks/{id}/presenter", "audienceUrl");
        view.Status.ShouldBe(200, view.Body);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(id);
        await presenter.IsLiveAsync();
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        var audience = new PlayerPage(await Page.Context.NewPageAsync());
        await audience.Root.Page.GotoAsync(view.Value.ShouldNotBeNull());
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.Code).ToHaveTextAsync(new Regex(@"\d"));
        var live = await PollJourney.ReadAsync(Page, $"/api/decks/{id}/live", "joinUrl");
        live.Status.ShouldBe(200, live.Body);

        await using var phone = await LiveDeck.OpenPhoneAsync(Browser);
        var guestPage = await phone.NewPageAsync();
        await Talks.FollowAsync(guestPage, live.Value.ShouldNotBeNull());
        var card = new GuestPollCard(guestPage);
        await Talks.MoveToAsync(presenter, audience, SLIDES, "ArrowRight", POLL);
        var panel = new PollPanel(Page);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await card.Option(PICKED).ClickAsync();
        await Expect(card.Sent).ToBeVisibleAsync();
        await panel.Close.ClickAsync();
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await presenter.Live.End.ClickAsync();
        await Expect(presenter.Live.GoLive).ToBeVisibleAsync();
        return new Deck(id, slug);
    }

    private sealed record Deck(string Id, string Slug);
}
