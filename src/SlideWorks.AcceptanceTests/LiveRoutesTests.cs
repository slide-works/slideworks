using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// Going live (J2) through the real server: the owner's POST opens a session, and the player screen's join object says live.
// The deck is made through the API with the browser's own session, as the web app does.
public class LiveRoutesTests : AcceptanceTest
{
    [Test]
    public async Task GoLive_WhenOwnerPosts_ShouldShowJoinLiveOnPlayerScreen()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var id = await PostJsonAsync("/api/decks", new { title = $"Live {Guid.NewGuid():N}" }, 201, "id");
        var before = await Page.APIRequest.GetAsync($"/api/play/{id}");
        var after = await PostLiveAsync(id);

        var play = JsonDocument.Parse(await (await Page.APIRequest.GetAsync($"/api/play/{id}")).TextAsync()).RootElement;

        JsonDocument.Parse(await before.TextAsync()).RootElement.GetProperty("join").GetProperty("live").GetBoolean().ShouldBeFalse();
        after.GetProperty("live").GetBoolean().ShouldBeTrue();
        var join = play.GetProperty("join");
        join.GetProperty("live").GetBoolean().ShouldBeTrue();
        join.GetProperty("code").GetString().ShouldBe(after.GetProperty("code").GetString());
        join.GetProperty("joinUrl").GetString().ShouldBe($"http://localhost:{new Uri(AcceptanceApp.BaseUrl).Port}/{join.GetProperty("code").GetString()}");
        join.GetProperty("hostLabel").GetString().ShouldBe($"localhost:{new Uri(AcceptanceApp.BaseUrl).Port}");
    }

    private async Task<JsonElement> PostLiveAsync(string id)
    {
        var response = await Page.APIRequest.PostAsync($"/api/decks/{id}/live", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    private async Task<string> PostJsonAsync(string path, object body, int status, string field)
    {
        var response = await Page.APIRequest.PostAsync(path, new APIRequestContextOptions
        {
            DataObject = body,
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(status, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty(field).GetString()!;
    }
}
