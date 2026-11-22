using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The join slide and the join bar on the big screen (SW-5 J6, D5). The deck is seeded through add_slide and the live and
// look APIs; the assertions use the player page object's join members.
public class JoinSlideTests : AcceptanceTest
{
    [Test]
    public async Task Present_WhenLiveWithJoinBar_ShouldShowBarOnContentSlide()
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync($"Join slide {Guid.NewGuid():N}");
        var deck = new DeckPage(Page);
        await Expect(Page).ToHaveURLAsync(new Regex(@"/decks/[0-9A-Za-z]{12}$"));
        var deckId = Page.Url[(Page.Url.LastIndexOf('/') + 1)..];
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            await AgentSeed.CallAsync(agent, "add_slide", new Dictionary<string, object?> { ["deck"] = deckId, ["layout"] = "join", ["slots"] = new { } });
            await AgentSeed.CallAsync(agent, "add_slide", new Dictionary<string, object?>
            {
                ["deck"] = deckId,
                ["layout"] = "bullets",
                ["slots"] = new { headline = "Why small batches", items = new[] { "Merge every day" } },
            });
        }
        await SendAsync(HttpMethod.Put.Method, $"/api/decks/{deckId}/look", new { chrome = new { joinBar = true } });
        await SendAsync(HttpMethod.Post.Method, $"/api/decks/{deckId}/live", null);
        var player = new PlayerPage(Page);
        await Page.ReloadAsync();
        await Expect(deck.Present).ToBeVisibleAsync();

        await deck.Present.ClickAsync();

        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.JoinSlide(1)).ToBeVisibleAsync();
        await Expect(player.JoinQr(1)).ToBeVisibleAsync();
        await Expect(player.JoinCode(1)).ToHaveTextAsync(new Regex(@"^\d{3} \d{3}$"));
        await Expect(player.JoinCount(1)).ToHaveTextAsync("0 joined");
        await Expect(player.JoinBar(1)).ToHaveCountAsync(0);
        // The count is the player's own position and shows before the presentation join is answered; a key in that window is
        // dropped (the player's moves are frozen until the join lands), so wait until the player is driving the live talk.
        await Expect(player.Player).ToHaveAttributeAsync("data-driving", "true");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("2 / 2");
        await Expect(player.JoinBar(2)).ToBeVisibleAsync();
        await Expect(player.JoinBarCode(2)).ToHaveTextAsync(new Regex(@"^\d{3} \d{3}$"));
        await Expect(player.JoinBarQr(2)).ToBeVisibleAsync();
    }

    private async Task SendAsync(string method, string path, object? body)
    {
        var response = await Page.APIRequest.FetchAsync(path, new APIRequestContextOptions
        {
            Method = method,
            DataObject = body,
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBeInRange(200, 204, await response.TextAsync());
    }
}
