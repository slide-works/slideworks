using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// K4: GET /api/decks/{id} is the owner's deck page (deck-page.json): the live slides with notes and links, the sharing
// line and the change log, newest first and at most 50 entries. Anyone else gets 404.
[TestFixture]
public class DeckPageTests : ApiTestFixture
{
    [Test]
    public async Task Get_ShouldReturnSlidesNotesActivityMatchingShape()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent, "Ship smaller", DeckSeed.SAMPLE_PARTS, "Sam Rivera");
        await DeckSeed.AddSlideAsync(agent, deck, "title", """{ "title": "Ship smaller" }""", part: 1, notes: "Open with the story.");
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "Why small", "items": ["a", "b", "c"], "reveal": "one-by-one" }""", part: 2);
        var longQuote = string.Join(" ", Enumerable.Repeat("Ship small.", 9));
        await DeckSeed.AddQuoteAsync(agent, deck, longQuote);
        Clock.Advance(TimeSpan.FromMinutes(5));

        var response = await Client.GetAsync($"/api/decks/{deck}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var page = document.RootElement;
        JsonShape.AssertMatches(page, "deck-page.json", ScreenShapes.DeckPage);
        page.GetProperty("id").GetString().ShouldBe(deck);
        page.GetProperty("slug").GetString().ShouldBe("ship-smaller");
        page.GetProperty("title").GetString().ShouldBe("Ship smaller");
        page.GetProperty("author").GetString().ShouldBe("Sam Rivera");
        page.GetProperty("isOwner").GetBoolean().ShouldBeTrue();
        page.GetProperty("playUrl").GetString().ShouldBe($"/d/{deck}/ship-smaller");
        page.GetProperty("sharing").GetProperty("level").GetString().ShouldBe("private");
        page.GetProperty("sharing").GetProperty("viewerCount").GetInt32().ShouldBe(0);
        page.GetProperty("parts").EnumerateArray().Select(p => p.GetString()).ShouldBe(DeckSeed.SAMPLE_PARTS);

        var slides = page.GetProperty("slides").EnumerateArray().ToList();
        slides.Select(s => s.GetProperty("number").GetInt32()).ShouldBe([1, 2, 3]);
        slides.Select(s => s.GetProperty("layout").GetString()).ShouldBe(["title", "bullets", "quote"]);
        slides.Select(s => s.GetProperty("link").GetString()).ShouldBe([$"/d/{deck}/ship-smaller#1", $"/d/{deck}/ship-smaller#2", $"/d/{deck}/ship-smaller#3"]);
        slides[0].GetProperty("headline").GetString().ShouldBe("Ship smaller");
        slides[0].GetProperty("notes").GetString().ShouldBe("Open with the story.");
        slides[0].GetProperty("part").GetInt32().ShouldBe(1);
        slides[0].GetProperty("slots").GetProperty("title").GetString().ShouldBe("Ship smaller");
        slides[1].GetProperty("headline").GetString().ShouldBe("Why small");
        slides[1].GetProperty("steps").GetInt32().ShouldBe(3);
        slides[1].GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
        slides[2].GetProperty("headline").GetString().ShouldBe(longQuote[..80]);
        slides[2].GetProperty("part").ValueKind.ShouldBe(JsonValueKind.Null);

        var activity = page.GetProperty("activity").EnumerateArray().ToList();
        activity.Select(a => a.GetProperty("summary").GetString())
            .ShouldBe(["added slide 3 (quote)", "added slide 2 (bullets)", "added slide 1 (title)", "Created from the talk template with the Cobalt palette"]);
        activity.ShouldAllBe(a => a.GetProperty("actor").GetString() == "Claude"
            && a.GetProperty("actorKind").GetString() == "agent"
            && a.GetProperty("via").GetString() == "mcp"
            && a.GetProperty("atLabel").GetString() == "5 min ago");
        activity.Select(a => a.GetProperty("tool").GetString()).ShouldBe(["add_slide", "add_slide", "add_slide", "create_deck"]);
        body.ShouldContain("\"level\":\"private\"", Case.Sensitive);
        body.ShouldContain("\"actorKind\":\"agent\",\"via\":\"mcp\"", Case.Sensitive);
    }

    [Test]
    public async Task Get_WhenMoreThanFiftyChanges_ShouldReturnNewestFifty()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        for (var i = 1; i <= 50; i++)
        {
            await DeckSeed.AddQuoteAsync(agent, deck, $"Quote {i}");
        }

        var activity = await DeckSeed.ActivityAsync(Client, deck);

        activity.Count.ShouldBe(50);
        activity[0].GetProperty("summary").GetString().ShouldBe("added slide 50 (quote)");
        activity[49].GetProperty("summary").GetString().ShouldBe("added slide 1 (quote)");
    }

    [Test]
    public async Task Get_WhenNotOwner_ShouldReturn404()
    {
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var alexDeck = await DeckSeed.CreateDeckAsync(alexAgent);

        var forbidden = await Client.GetAsync($"/api/decks/{alexDeck}");
        var unknown = await Client.GetAsync("/api/decks/nOsUcHdEcK00");

        forbidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await forbidden.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unknown.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
    }

    [Test]
    public async Task Get_WhenSignedOut_ShouldReturn401()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/decks/{deck}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
