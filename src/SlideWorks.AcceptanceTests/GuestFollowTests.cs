using System.Text.Json;
using SlideWorks.AcceptanceTests.Journeys;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// A phone following a live talk (SW-5 G1a). The deck is built through the agent's tools and gone live through the API,
// as the owner does; the owner's plain player then moves on and the phone, a second browser context at 390×844, follows.
public class GuestFollowTests : AcceptanceTest
{
    private AgentOAuthClient _oauth = null!;

    [OneTimeSetUp]
    public async Task RegisterAgent() => _oauth = await AgentOAuthClient.RegisterAsync(AcceptanceApp.BaseUrl, "Claude");

    [OneTimeTearDown]
    public void ForgetAgent() => _oauth.Dispose();

    // red until SW-217: the real relay is what carries the presenter's position to the phone.
    [Test]
    public async Task Follow_WhenPresenterMoves_ShouldShowNewSlide()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var deckId = (await agent.CallAsync("create_deck", new { title = $"Follow {Guid.NewGuid():N}" })).GetProperty("deck").GetString()!;
        await agent.CallAsync("add_slide", new { deck = deckId, layout = "quote", slots = new { quote = "Start small.", attribution = "Sam Rivera" } });
        await agent.CallAsync("add_slide", new { deck = deckId, layout = "quote", slots = new { quote = "Then keep going.", attribution = "Sam Rivera" } });
        var live = await PostLiveAsync(deckId);
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync("1 / 2");

        await using var phoneContext = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = AcceptanceApp.BaseUrl,
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });
        var phone = new GuestPage(await phoneContext.NewPageAsync());
        await phone.GotoAsync(live.GetProperty("code").GetString()!);
        await phone.IsFollowingAsync();
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "1");
        await Expect(phone.Progress).ToHaveTextAsync("1 / 2");
        await Expect(phone.ActivityWaiting).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("ArrowRight");

        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");
        await Expect(phone.Progress).ToHaveTextAsync("2 / 2");
    }

    private async Task<JsonElement> PostLiveAsync(string deckId)
    {
        var response = await Page.APIRequest.PostAsync($"/api/decks/{deckId}/live", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }
}
