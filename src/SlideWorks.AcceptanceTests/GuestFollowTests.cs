using SlideWorks.AcceptanceTests.Journeys;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// A phone following a live talk (SW-5 G1a). The deck is built through the agent's tools and gone live through the API,
// as the owner does (LiveDeck); the owner's plain player then moves on and the phone, a second browser context at
// 390×844, follows.
public class GuestFollowTests : AcceptanceTest
{
    private AgentOAuthClient _oauth = null!;

    [OneTimeSetUp]
    public async Task RegisterAgent() => _oauth = await AgentOAuthClient.RegisterAsync(AcceptanceApp.BaseUrl, "Claude");

    [OneTimeTearDown]
    public void ForgetAgent() => _oauth.Dispose();

    [Test]
    public async Task Follow_WhenPresenterMoves_ShouldShowNewSlide()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var live = await LiveDeck.StartAsync(agent, Page);
        var player = new PlayerPage(Page);
        await player.GotoAsync(live.DeckId);
        await Expect(player.Count).ToHaveTextAsync("1 / 2");

        await using var phoneContext = await LiveDeck.OpenPhoneAsync(Browser);
        var phone = new GuestPage(await phoneContext.NewPageAsync());
        await phone.GotoAsync(live.Code);
        await phone.IsFollowingAsync();
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "1");
        await Expect(phone.Progress).ToHaveTextAsync("1 / 2");
        await Expect(phone.ActivityWaiting).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("ArrowRight");

        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");
        await Expect(phone.Progress).ToHaveTextAsync("2 / 2");
    }
}
