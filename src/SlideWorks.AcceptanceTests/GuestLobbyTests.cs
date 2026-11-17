using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The guest phone before Go live and after End (J3): both are plain reloads of /j/{code}, so no hub is needed. The deck is
// made through the API with the browser's own session and gets its join slide through the MCP add_slide tool.
public class GuestLobbyTests : AcceptanceTest
{
    [Test]
    public async Task Open_WhenDeckHasJoinSlideAndNotLive_ShouldShowLobby()
    {
        var code = await CreateDeckWithJoinSlideAsync();
        var guest = new GuestPage(Page);

        await guest.GotoAsync(code);

        await Expect(guest.Lobby).ToBeVisibleAsync();
        await Expect(guest.Ended).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Open_WhenSessionEndedRecently_ShouldShowThanks()
    {
        var code = await CreateDeckWithJoinSlideAsync(endSession: true);
        var guest = new GuestPage(Page);

        await guest.GotoAsync(code);

        await Expect(guest.Ended).ToBeVisibleAsync();
        await Expect(guest.Lobby).ToHaveCountAsync(0);
    }

    // Signs in as Sam, makes a deck with a join slide, optionally goes live and ends, and returns the deck's join code.
    private async Task<string> CreateDeckWithJoinSlideAsync(bool endSession = false)
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var created = await SendAsync("POST", "/api/decks", new { title = $"Lobby {Guid.NewGuid():N}" }, 201);
        var id = created.GetProperty("id").GetString()!;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            await AgentSeed.CallAsync(agent, "add_slide", new Dictionary<string, object?> { ["deck"] = id, ["layout"] = "join", ["slots"] = new { } });
        }
        if (endSession)
        {
            await SendAsync("POST", $"/api/decks/{id}/live", null, 200);
            await SendAsync("DELETE", $"/api/decks/{id}/live", null, 204);
        }
        var live = await SendAsync("GET", $"/api/decks/{id}/live", null, 200);
        return live.GetProperty("code").GetString()!;
    }

    private async Task<JsonElement> SendAsync(string method, string path, object? body, int status)
    {
        var response = await Page.APIRequest.FetchAsync(path, new APIRequestContextOptions
        {
            Method = method,
            DataObject = body,
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(status, await response.TextAsync());
        var text = await response.TextAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}
