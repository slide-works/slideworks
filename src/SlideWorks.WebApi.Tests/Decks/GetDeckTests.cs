using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// K6: get_deck returns the owner's whole deck, with each live slide's revision and notes.
[TestFixture]
public class GetDeckTests : ApiTestFixture
{
    [Test]
    public async Task GetDeck_ShouldReturnSlidesWithNotesAndRevision()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, "Ship smaller", DeckSeed.SAMPLE_PARTS, "Sam Rivera");
        await DeckSeed.AddSlideAsync(agent, deck, "title", """{ "title": "Ship smaller" }""", notes: "Open with the story.");
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "Why", "items": ["a", "b"], "reveal": "one-by-one" }""", part: 2);

        var (isError, json) = await agent.CallToolAsync("get_deck", new { deck });

        isError.ShouldBeFalse(json.ToString());
        json.GetProperty("id").GetString().ShouldBe(deck);
        json.GetProperty("title").GetString().ShouldBe("Ship smaller");
        json.GetProperty("author").GetString().ShouldBe("Sam Rivera");
        json.GetProperty("parts").EnumerateArray().Select(p => p.GetString()).ShouldBe(DeckSeed.SAMPLE_PARTS);
        json.GetProperty("sharing").GetString().ShouldBe("private");
        json.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller");
        var slides = json.GetProperty("slides").EnumerateArray().ToList();
        slides.Count.ShouldBe(2);
        slides[0].EnumerateObject().Select(p => p.Name).ShouldBe(["number", "layout", "part", "deep_dive", "surface", "steps", "revision", "slots", "notes"], ignoreOrder: true);
        slides[0].GetProperty("number").GetInt32().ShouldBe(1);
        slides[0].GetProperty("layout").GetString().ShouldBe("title");
        slides[0].GetProperty("part").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        slides[0].GetProperty("revision").GetInt64().ShouldBe(1);
        slides[0].GetProperty("notes").GetString().ShouldBe("Open with the story.");
        slides[0].GetProperty("slots").GetProperty("title").GetString().ShouldBe("Ship smaller");
        slides[1].GetProperty("number").GetInt32().ShouldBe(2);
        slides[1].GetProperty("part").GetInt32().ShouldBe(2);
        slides[1].GetProperty("steps").GetInt32().ShouldBe(2);
        slides[1].GetProperty("revision").GetInt64().ShouldBe(2);
        slides[1].GetProperty("notes").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Test]
    public async Task GetDeck_WhenOtherPresenter_ShouldSayDeckNotFound()
    {
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var alexDeck = await DeckSeed.CreateDeckAsync(alexAgent);
        await using var samAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await samAgent.CallToolAsync("get_deck", new { deck = alexDeck });
        var (unknownIsError, unknown) = await samAgent.CallToolAsync("get_deck", new { deck = "nOsUcHdEcK00" });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("Deck not found");
        unknownIsError.ShouldBeTrue();
        unknown.GetString().ShouldBe("Deck not found");
    }
}
