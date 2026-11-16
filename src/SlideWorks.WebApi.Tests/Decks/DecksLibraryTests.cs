using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// K4: GET /api/decks is the decks library screen (decks-library.json): the caller's decks newest change first, the decks
// shared with them by name, and the "connect an agent" card. The fixture's app is shared, so each test picks out the
// decks it made, or reads as a person who has made none.
[TestFixture]
public class DecksLibraryTests : ApiTestFixture
{
    [Test]
    public async Task Get_WhenTwoDecks_ShouldListNewestFirstMatchingShape()
    {
        var older = await PostDeckAsync("Q4 platform review");
        Clock.Advance(TimeSpan.FromHours(1));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var newer = await DeckSeed.CreateDeckAsync(agent, "Ship smaller");
        await DeckSeed.AddQuoteAsync(agent, newer, "Small batches beat big plans.");
        await DeckSeed.AddQuoteAsync(agent, newer, "Ship it on Friday.");
        Clock.Advance(TimeSpan.FromMinutes(2));

        var (body, library) = await GetLibraryAsync(Client);

        JsonShape.AssertMatches(library, "decks-library.json", ScreenShapes.DecksLibrary);
        var rows = library.GetProperty("decks").EnumerateArray().Where(r => r.GetProperty("id").GetString() is var id && (id == older || id == newer)).ToList();
        rows.Select(r => r.GetProperty("id").GetString()).ShouldBe([newer, older]);

        var ship = rows[0];
        ship.GetProperty("slug").GetString().ShouldBe("ship-smaller");
        ship.GetProperty("title").GetString().ShouldBe("Ship smaller");
        ship.GetProperty("subtitle").ValueKind.ShouldBe(JsonValueKind.Null);
        ship.GetProperty("slideCount").GetInt32().ShouldBe(2);
        ship.GetProperty("sharing").GetString().ShouldBe("private");
        ship.GetProperty("viewerCount").GetInt32().ShouldBe(0);
        ship.GetProperty("updatedLabel").GetString().ShouldBe("2 min ago");
        ship.GetProperty("playUrl").GetString().ShouldBe($"/d/{newer}/ship-smaller");
        ship.GetProperty("lastChange").GetProperty("summary").GetString().ShouldBe("added slide 2 (quote)");

        var review = rows[1];
        review.GetProperty("title").GetString().ShouldBe("Q4 platform review");
        review.GetProperty("slideCount").GetInt32().ShouldBe(0);
        review.GetProperty("updatedLabel").GetString().ShouldBe("1 h ago");
        review.GetProperty("playUrl").GetString().ShouldBe($"/d/{older}/q4-platform-review");
        var lastChange = review.GetProperty("lastChange");
        lastChange.GetProperty("actor").GetString().ShouldBe(TestUser.Sam.Name);
        lastChange.GetProperty("actorKind").GetString().ShouldBe("person");
        lastChange.GetProperty("via").GetString().ShouldBe("web");
        lastChange.GetProperty("summary").GetString().ShouldBe("created the deck");

        body.ShouldContain("\"sharing\":\"private\"", Case.Sensitive);
        body.ShouldContain("\"actorKind\":\"person\"", Case.Sensitive);
        body.ShouldContain("\"via\":\"web\"", Case.Sensitive);
    }

    [Test]
    public async Task Get_WhenAgentChangedLast_ShouldShowAgentViaMcp()
    {
        var deck = await PostDeckAsync("Onboarding: week one");
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Cursor");
        await DeckSeed.AddQuoteAsync(agent, deck, "Pair on day one.");

        var (body, library) = await GetLibraryAsync(Client);

        var row = library.GetProperty("decks").EnumerateArray().Single(r => r.GetProperty("id").GetString() == deck);
        var lastChange = row.GetProperty("lastChange");
        lastChange.GetProperty("actor").GetString().ShouldBe("Cursor");
        lastChange.GetProperty("actorKind").GetString().ShouldBe("agent");
        lastChange.GetProperty("via").GetString().ShouldBe("mcp");
        lastChange.GetProperty("summary").GetString().ShouldBe("added slide 1 (quote)");
        row.GetProperty("slideCount").GetInt32().ShouldBe(1);
        body.ShouldContain("\"actorKind\":\"agent\",\"via\":\"mcp\"", Case.Sensitive);
    }

    // Mira makes nothing in this fixture. She has no agent token in the database, so the card shows.
    [Test]
    public async Task Get_WhenNoDecks_ShouldReturnEmptyListsAndConnectBanner()
    {
        using var mira = ClientFor(TestUser.Mira);

        var (_, library) = await GetLibraryAsync(mira);

        JsonShape.AssertMatches(library, "decks-library.json", ScreenShapes.DecksLibrary);
        library.GetProperty("decks").GetArrayLength().ShouldBe(0);
        library.GetProperty("sharedWithMe").GetArrayLength().ShouldBe(0);
        library.GetProperty("connect").GetProperty("show").GetBoolean().ShouldBeTrue();
        library.GetProperty("connect").GetProperty("mcpUrl").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/mcp");
    }

    // Another presenter's private deck is neither Sam's nor shared with him. The populated case needs named sharing and is
    // the end-to-end journey's.
    [Test]
    public async Task Get_WhenSharedWithMeEmpty_ShouldReturnEmptyList()
    {
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var alexDeck = await DeckSeed.CreateDeckAsync(alexAgent, "Alex's plans");

        var (_, library) = await GetLibraryAsync(Client);

        library.GetProperty("sharedWithMe").GetArrayLength().ShouldBe(0);
        library.GetProperty("decks").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldNotContain(alexDeck);
    }

    [Test]
    public async Task Get_WhenSignedOut_ShouldReturn401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync("/api/decks");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Not signed in");
    }

    private async Task<string> PostDeckAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<(string Body, JsonElement Library)> GetLibraryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/decks");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return (body, document.RootElement.Clone());
    }
}
