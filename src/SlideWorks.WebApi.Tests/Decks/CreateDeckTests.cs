using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// K5: POST /api/decks and create_deck make a private deck owned by the caller (or the presenter an agent acts for), and
// record one change-log entry each.
[TestFixture]
public class CreateDeckTests : ApiTestFixture
{
    [Test]
    public async Task Post_WhenSignedIn_ShouldCreatePrivateDeck()
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Ship smaller" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;
        id.Length.ShouldBe(12);
        id.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c));
        created.GetProperty("slug").GetString().ShouldBe("ship-smaller");
        created.GetProperty("playUrl").GetString().ShouldBe($"/d/{id}/ship-smaller");
        created.EnumerateObject().Select(p => p.Name).ShouldBe(["id", "slug", "playUrl"], ignoreOrder: true);

        (await Client.GetAsync($"/api/play/{id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var alex = ClientFor(TestUser.Alex);
        (await alex.GetAsync($"/api/play/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var anonymous = ClientFor(TestUser.Anonymous);
        (await anonymous.GetAsync($"/api/play/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Post_WhenSignedIn_ShouldRecordPersonActivityFromTheWeb()
    {
        var id = await PostDeckAsync("Ship smaller");

        Clock.Advance(TimeSpan.FromMinutes(3));

        var entry = (await DeckSeed.ActivityAsync(Client, id)).ShouldHaveSingleItem();
        entry.GetProperty("actor").GetString().ShouldBe(TestUser.Sam.Name);
        entry.GetProperty("actorKind").GetString().ShouldBe("person");
        entry.GetProperty("via").GetString().ShouldBe("web");
        entry.GetProperty("tool").ValueKind.ShouldBe(JsonValueKind.Null);
        entry.GetProperty("summary").GetString().ShouldBe("Created from the talk template with the Cobalt palette");
        entry.GetProperty("atLabel").GetString().ShouldBe("3 min ago");
    }

    [TestCase("Café — ship it, now!", "cafe-ship-it-now")]
    [TestCase("  Q4: 10× faster  ", "q4-10-faster")]
    [TestCase("日本語", "deck")]
    public async Task Post_WhenTitleHasPunctuationOrAccents_ShouldMakeAPlainSlug(string title, string slug)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title });

        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString().ShouldBe(slug);
    }

    [TestCase("{}")]
    [TestCase("""{ "title": "   " }""")]
    [TestCase("""{ "title": null }""")]
    public async Task Post_WhenTitleMissing_ShouldReturn400(string body)
    {
        var response = await Client.PostAsync("/api/decks", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("title: required");
    }

    [Test]
    public async Task Post_WhenTitleTooLong_ShouldReturn400()
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = new string('a', 121) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("title: at most 120 characters");
    }

    [Test]
    public async Task Post_WhenSignedOut_ShouldReturn401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.PostAsJsonAsync("/api/decks", new { title = "Ship smaller" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Not signed in");
    }

    [Test]
    public async Task CreateDeck_WhenAgent_ShouldRecordAgentActivity()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");

        var (isError, json) = await agent.CallToolAsync("create_deck", new { title = "Ship smaller", parts = DeckSeed.SAMPLE_PARTS });

        isError.ShouldBeFalse(json.ToString());
        var id = json.GetProperty("deck").GetString()!;
        var entry = (await DeckSeed.ActivityAsync(Client, id)).ShouldHaveSingleItem();
        entry.GetProperty("actor").GetString().ShouldBe("Claude");
        entry.GetProperty("actorKind").GetString().ShouldBe("agent");
        entry.GetProperty("via").GetString().ShouldBe("mcp");
        entry.GetProperty("tool").GetString().ShouldBe("create_deck");
        entry.GetProperty("summary").GetString().ShouldBe("Created from the talk template with the Cobalt palette");
    }

    // The agent's deck belongs to the presenter it acts for: Sam opens it in the browser.
    [Test]
    public async Task CreateDeck_WhenAgent_ShouldReturnDeckAbsoluteUrlAndPrivateForThePresenter()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (_, json) = await agent.CallToolAsync("create_deck", new { title = "Ship smaller", author = "Sam Rivera" });

        var id = json.GetProperty("deck").GetString()!;
        json.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{id}/ship-smaller");
        json.GetProperty("sharing").GetString().ShouldBe("private");
        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{id}");
        play.GetProperty("author").GetString().ShouldBe("Sam Rivera");
    }

    [Test]
    public async Task CreateDeck_OnTheWire_ShouldSendTheResultAsStructuredContent()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "create_deck", """{"title":"Ship smaller"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(["deck", "url", "sharing", "look", "layouts", "outline", "house_style"], ignoreOrder: true);
        McpWire.TextOf(result).ShouldBe(structured.GetRawText());
    }

    [TestCase("""{"title":""}""", "title: required")]
    [TestCase("""{"title":"Ship","parts":["One",""]}""", "parts[1]: required")]
    [TestCase("""{"title":"Ship","parts":["1","2","3","4","5","6","7","8","9"]}""", "parts: at most 8 items")]
    public async Task CreateDeck_WhenArgsInvalid_ShouldReturnToolError(string args, string error)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "create_deck", args);

        result.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(result).ShouldBe(error);
    }

    private async Task<string> PostDeckAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
}
