using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Journeys;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The owner goes live and ends from the plain player's live chip (SW-5 J2, J6, D18).
public class LiveFlowTests : AcceptanceTest
{
    private AgentOAuthClient _oauth = null!;

    [OneTimeSetUp]
    public async Task RegisterAgent() => _oauth = await AgentOAuthClient.RegisterAsync(AcceptanceApp.BaseUrl, "Claude");

    [OneTimeTearDown]
    public void ForgetAgent() => _oauth.Dispose();

    [Test]
    public async Task GoLive_WhenOwnerClicks_ShouldShowCodeAndEndReturnsToGoLive()
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync($"Live flow {Guid.NewGuid():N}");
        var deck = new DeckPage(Page);
        await Expect(Page).ToHaveURLAsync(new Regex(@"/decks/[0-9A-Za-z]{12}$"));
        await Expect(deck.Present).ToBeVisibleAsync();
        await deck.Present.ClickAsync();
        var player = new PlayerPage(Page);
        await Expect(player.Player).ToBeVisibleAsync();

        await player.LiveChip.GoLive.ClickAsync();

        await Expect(player.LiveChip.Code).ToHaveTextAsync(new Regex(@"^\d{3} \d{3}$"));
        await Expect(player.LiveChip.Count).ToHaveTextAsync("0 joined");
        await Expect(player.LiveChip.Error).ToHaveCountAsync(0);

        await player.LiveChip.End.ClickAsync();

        await Expect(player.LiveChip.GoLive).ToBeVisibleAsync();
        await Expect(player.LiveChip.End).ToHaveCountAsync(0);
    }

    [Test]
    public async Task JoinBarToggle_WhenLive_ShouldDrawAndRemoveTheBarInTheSamePlayer()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var deckId = (await agent.CallAsync("create_deck", new { title = $"Join bar flow {Guid.NewGuid():N}" })).GetProperty("deck").GetString()!;
        await agent.CallAsync("add_slide", new { deck = deckId, layout = "quote", slots = new { quote = "Start small.", attribution = "Sam Rivera" } });
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync("1 / 1");
        await player.LiveChip.GoLive.ClickAsync();
        await Expect(player.LiveChip.End).ToBeVisibleAsync();
        await Page.EvaluateAsync("() => { window.sameDocument = true; }");

        await player.LiveChip.JoinBarToggle.ClickAsync();

        await Expect(player.JoinBar(1)).ToBeVisibleAsync();
        await Expect(player.LiveChip.JoinBarToggle).ToHaveAttributeAsync("aria-pressed", "true");
        (await Page.EvaluateAsync<bool>("() => window.sameDocument === true")).ShouldBeTrue();

        await player.LiveChip.JoinBarToggle.ClickAsync();

        await Expect(player.JoinBar(1)).ToHaveCountAsync(0);
        await Expect(player.LiveChip.JoinBarToggle).ToHaveAttributeAsync("aria-pressed", "false");
    }
}
