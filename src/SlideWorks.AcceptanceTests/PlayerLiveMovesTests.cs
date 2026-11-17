using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The owner's plain player while live is one more presenter-role client of the talk (SW-5 D18, SW-230). Keys pressed with
// no wait between them are three moves, each from the one before, and none is lost or snapped back: in the middle of a
// talk, and in the first round trip after the player opens on a deck that is already live.
public class PlayerLiveMovesTests : AcceptanceTest
{
    private const int SLIDES = 5;

    // Delivers every hub message to the page 300 ms late, in order, as a slow network would, and counts the position updates
    // the page has sent.
    private const string SLOW_HUB_ANSWERS =
        """
        (() => {
          const Native = window.WebSocket;
          window.__updatesSent = 0;
          class SlowSocket extends Native {
            send(data) {
              if (typeof data === 'string' && data.includes('UpdatePresentation')) window.__updatesSent += 1;
              return super.send(data);
            }
            set onmessage(handler) {
              super.onmessage = handler ? (event) => setTimeout(() => handler.call(this, event), 300) : handler;
            }
            get onmessage() {
              return super.onmessage;
            }
          }
          window.WebSocket = SlowSocket;
        })();
        """;

    [Test]
    public async Task Moves_WhenThreeKeysArePressedWithNoWaitBetween_ShouldMoveThePlayerAndThePhoneByThree()
    {
        var deckId = await CreateDeckAsync();
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await player.LiveChip.GoLive.ClickAsync();
        await Expect(player.LiveChip.End).ToBeVisibleAsync();
        await using var phoneContext = await OpenPhoneContextAsync();
        var phone = new GuestPage(await phoneContext.NewPageAsync());
        await phone.GotoUrlAsync(await JoinUrlAsync(deckId));
        await phone.IsFollowingAsync();
        // One move with a wait: the player has joined the talk by the time the phone shows it.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");

        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");

        await Expect(player.Count).ToHaveTextAsync($"5 / {SLIDES}");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "5");
    }

    // The player opens on slide 2 of a deck that is live with nobody driving, so it publishes its own position first. The
    // hub's answers reach the page 300 ms late, which holds that first publish open; the keys go in once it has been sent.
    [Test]
    public async Task Moves_WhenThreeKeysArePressedWhileTheFirstPublishIsInFlight_ShouldMoveThePlayerAndThePhoneByThree()
    {
        var deckId = await CreateDeckAsync();
        await GoLiveAsync(deckId);
        await using var phoneContext = await OpenPhoneContextAsync();
        var phone = new GuestPage(await phoneContext.NewPageAsync());
        await phone.GotoUrlAsync(await JoinUrlAsync(deckId));
        await phone.IsFollowingAsync();
        await Page.AddInitScriptAsync(SLOW_HUB_ANSWERS);
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId, hash: "#2");
        await Page.WaitForFunctionAsync("() => window.__updatesSent >= 1");

        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");

        await Expect(player.Count).ToHaveTextAsync($"5 / {SLIDES}");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "5");
        await Expect(player.Count).ToHaveTextAsync($"5 / {SLIDES}");
    }

    // Sam's deck of five quotes, with Sam signed in on the test's page.
    private async Task<string> CreateDeckAsync()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var response = await Page.APIRequest.PostAsync("/api/decks", new APIRequestContextOptions
        {
            DataObject = new { title = $"Rapid keys {Guid.NewGuid():N}" },
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(201, await response.TextAsync());
        var id = JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("id").GetString().ShouldNotBeNull();
        await AgentSeed.AddQuotesAsync(TestUser.Sam, id, "One.", "Two.", "Three.", "Four.", "Five.");
        return id;
    }

    private async Task GoLiveAsync(string deckId)
    {
        var response = await Page.APIRequest.PostAsync($"/api/decks/{deckId}/live", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
    }

    private async Task<string> JoinUrlAsync(string deckId)
    {
        var response = await Page.APIRequest.GetAsync($"/api/decks/{deckId}/live");
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("joinUrl").GetString().ShouldNotBeNull();
    }

    private Task<IBrowserContext> OpenPhoneContextAsync() => Browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });
}
