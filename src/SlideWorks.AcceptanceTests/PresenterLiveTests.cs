using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Journeys;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The owner goes live from SW-4's presenter window and the plain player stays one more window of the same talk
// (SW-5 D19, SW-230): the window's live chip starts the presentation, a phone follows it, and opening the plain player
// afterwards neither moves the talk nor clears its black screen or timer.
public class PresenterLiveTests : AcceptanceTest
{
    private AgentOAuthClient _oauth = null!;

    [OneTimeSetUp]
    public async Task RegisterAgent() => _oauth = await AgentOAuthClient.RegisterAsync(AcceptanceApp.BaseUrl, "Claude");

    [OneTimeTearDown]
    public void ForgetAgent() => _oauth.Dispose();

    [Test]
    public async Task GoLive_WhenOwnerUsesThePresenterWindowAndThenOpensThePlayer_ShouldKeepOneTalkForThePhone()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var deckId = (await agent.CallAsync("create_deck", new { title = $"Presenter live {Guid.NewGuid():N}" })).GetProperty("deck").GetString()!;
        foreach (var text in new[] { "Start small.", "Then keep going.", "Then finish." })
        {
            await agent.CallAsync("add_slide", new { deck = deckId, layout = "quote", slots = new { quote = text, attribution = "Sam Rivera" } });
        }
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Expect(presenter.Count).ToHaveTextAsync("1 / 3");

        // Go live from the presenter window starts the presentation: no second Go live anywhere.
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.Code).ToHaveTextAsync(new Regex(@"^\d{3} \d{3}$"));
        await Expect(presenter.Live.Count).ToHaveTextAsync("0 joined");
        var code = (await presenter.Live.Code.InnerTextAsync()).Replace(" ", "", StringComparison.Ordinal);

        await using var phoneContext = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = AcceptanceApp.BaseUrl,
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });
        var phone = new GuestPage(await phoneContext.NewPageAsync());
        await phone.GotoAsync(code);
        await phone.IsFollowingAsync();
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "1");
        await Expect(presenter.Live.Count).ToHaveTextAsync("1 joined");

        // The phone follows moves made in the presenter window; a black screen and the running timer come along.
        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync("2 / 3");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");
        await presenter.PressAsync("b");
        await Expect(presenter.BlackBadge).ToBeVisibleAsync();
        await Expect(phone.Slide).ToHaveAttributeAsync("data-black", "true");
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "running");

        // The owner also opens the plain player in another tab. It adopts the talk: nothing in the presenter window moves.
        var playerTab = await Context.NewPageAsync();
        var player = new PlayerPage(playerTab);
        await player.GotoAsync(deckId);
        await Expect(player.LiveChip.End).ToBeVisibleAsync();
        await Expect(player.Count).ToHaveTextAsync("2 / 3");
        await Expect(presenter.Count).ToHaveTextAsync("2 / 3");
        await Expect(presenter.BlackBadge).ToBeVisibleAsync();
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "running");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");

        // A move in the player is followed by the presenter window and the phone, and carries the black screen through: the
        // room stays black on the new slide.
        await playerTab.Keyboard.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync("3 / 3");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "3");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-black", "true");
        await Expect(presenter.BlackBadge).ToBeVisibleAsync();

        // A move in the presenter window is followed by the player and the phone.
        await Page.BringToFrontAsync();
        await presenter.PressAsync("ArrowLeft");
        await Expect(player.Count).ToHaveTextAsync("2 / 3");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "running");

        // End from the player ends the session for both chips.
        await playerTab.BringToFrontAsync();
        await player.LiveChip.End.ClickAsync();
        await Expect(player.LiveChip.GoLive).ToBeVisibleAsync();
        await Expect(presenter.Live.GoLive).ToBeVisibleAsync();
        await Expect(phone.Ended).ToBeVisibleAsync();
    }

    [Test]
    public async Task End_WhenOwnerEndsFromThePresenterWindow_ShouldUpdateThePlayersChip()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var deckId = (await agent.CallAsync("create_deck", new { title = $"Presenter end {Guid.NewGuid():N}" })).GetProperty("deck").GetString()!;
        await agent.CallAsync("add_slide", new { deck = deckId, layout = "quote", slots = new { quote = "Start small.", attribution = "Sam Rivera" } });
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await player.LiveChip.GoLive.ClickAsync();
        await Expect(player.LiveChip.End).ToBeVisibleAsync();
        var presenterTab = await Context.NewPageAsync();
        var presenter = new PresenterPage(presenterTab);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Expect(presenter.Live.End).ToBeVisibleAsync();

        await presenter.Live.End.ClickAsync();

        await Expect(presenter.Live.GoLive).ToBeVisibleAsync();
        await Expect(player.LiveChip.GoLive).ToBeVisibleAsync();
    }
}
