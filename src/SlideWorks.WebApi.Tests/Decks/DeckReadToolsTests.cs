using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// K6: list_decks, get_deck_url, list_layouts and list_blocks. The list tools are checked on the raw JSON-RPC wire: their
// structuredContent must be an object ({decks}, {layouts}, {blocks}), because clients on the TypeScript SDK refuse an
// array there.
[TestFixture]
public class DeckReadToolsTests : ApiTestFixture
{
    [Test]
    public async Task GetDeckUrl_WhenSlideAndStep_ShouldReturnHashLink()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, "Ship smaller");
        await DeckSeed.AddQuoteAsync(agent, deck, "Small batches.");
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "Why", "items": ["a", "b", "c"], "reveal": "one-by-one" }""");

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_deck_url", $$"""{"deck":"{{deck}}","slide":2,"step":1}""");
        var (_, slideOnly) = await agent.CallToolAsync("get_deck_url", new { deck, slide = 2 });
        var (_, deckOnly) = await agent.CallToolAsync("get_deck_url", new { deck });
        var (_, lastStep) = await agent.CallToolAsync("get_deck_url", new { deck, slide = 2, step = 3 });

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var structured = result.GetProperty("structuredContent");
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(["url"]);
        structured.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller#2.1");
        McpWire.TextOf(result).ShouldBe(structured.GetRawText());
        slideOnly.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller#2");
        deckOnly.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller");
        lastStep.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller#2.3");
    }

    [Test]
    public async Task GetDeckUrl_WhenSlideOrStepOutOfRange_ShouldSayWhy()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Small batches.");

        (await ToolErrorAsync(agent, new { deck, slide = 2 })).ShouldBe("slide: no slide 2; the deck has 1");
        (await ToolErrorAsync(agent, new { deck, slide = 0 })).ShouldBe("slide: no slide 0; the deck has 1");
        (await ToolErrorAsync(agent, new { deck, slide = 1, step = 1 })).ShouldBe("step: slide 1 has steps 0 to 0");
        (await ToolErrorAsync(agent, new { deck, slide = 1, step = -1 })).ShouldBe("step: slide 1 has steps 0 to 0");
        (await ToolErrorAsync(agent, new { deck, step = 1 })).ShouldBe("step: needs a slide");
    }

    [Test]
    public async Task GetDeckUrl_WhenOtherPresenter_ShouldSayDeckNotFound()
    {
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var alexDeck = await DeckSeed.CreateDeckAsync(alexAgent);
        await using var samAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await samAgent.CallToolAsync("get_deck_url", new { deck = alexDeck });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("Deck not found");
    }

    [Test]
    public async Task ListLayouts_ShouldReturnNineteenWithSchemas()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "list_layouts", "{}");

        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(["layouts"]);
        McpWire.TextOf(result).ShouldBe(structured.GetRawText());
        var layouts = structured.GetProperty("layouts").EnumerateArray().ToList();
        layouts.Select(l => l.GetProperty("name").GetString()).ShouldBe(LayoutCatalogue.LayoutNames);
        foreach (var layout in layouts)
        {
            layout.EnumerateObject().Select(p => p.Name).ShouldBe(["name", "description", "dark", "stepsFrom", "schema"]);
            layout.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
            var schema = layout.GetProperty("schema");
            schema.GetProperty("$schema").GetString().ShouldBe("https://json-schema.org/draft/2020-12/schema");
            schema.GetProperty("type").GetString().ShouldBe("object");
            schema.GetProperty("properties").ValueKind.ShouldBe(JsonValueKind.Object);
        }
        layouts.Where(l => l.GetProperty("dark").GetBoolean()).Select(l => l.GetProperty("name").GetString()).ShouldBe(["title", "divider", "close", "join"]);
        var practice = layouts.Single(l => l.GetProperty("name").GetString() == "practice").GetProperty("schema");
        practice.GetProperty("properties").EnumerateObject().Select(p => p.Name).ShouldBe(LayoutCatalogue.Find("practice")!.Slots.Fields.Select(f => f.Name), ignoreOrder: true);
        practice.GetProperty("required").EnumerateArray().Select(r => r.GetString()).ShouldContain("do");
        layouts.Single(l => l.GetProperty("name").GetString() == "bullets").GetProperty("stepsFrom").GetString().ShouldBe("items");
        layouts.Single(l => l.GetProperty("name").GetString() == "quote").GetProperty("stepsFrom").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task ListBlocks_ShouldReturnFive()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "list_blocks", "{}");

        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(["blocks"]);
        McpWire.TextOf(result).ShouldBe(structured.GetRawText());
        var blocks = structured.GetProperty("blocks").EnumerateArray().ToList();
        blocks.Select(b => b.GetProperty("name").GetString()).ShouldBe(LayoutCatalogue.Blocks.Select(b => b.Name));
        blocks.Select(b => b.GetProperty("setByAgent").GetBoolean()).ShouldBe(LayoutCatalogue.Blocks.Select(b => b.SetByAgent));
        var callout = blocks[0].GetProperty("schema");
        callout.GetProperty("type").GetString().ShouldBe("object");
        callout.GetProperty("properties").GetProperty("tone").GetProperty("enum").EnumerateArray().Select(t => t.GetString()).ShouldBe(["accent", "positive", "negative", "inverse", "good", "bad", "info"]);
        blocks[1].GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
    }

    [Test]
    public async Task ListDecks_ShouldReturnOnlyCallersDecks()
    {
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var alexDeck = await DeckSeed.CreateDeckAsync(alexAgent, "Alex's plans");
        await using var mira = await McpTestClient.ConnectAsAsync(Factory, TestUser.Mira);
        var first = await DeckSeed.CreateDeckAsync(mira, "First deck");
        Clock.Advance(TimeSpan.FromMinutes(10));
        var second = await DeckSeed.CreateDeckAsync(mira, "Second deck");
        await DeckSeed.AddQuoteAsync(mira, second, "Ship it.");

        var result = await McpWire.CallToolAsync(Factory, mira.Tokens.AccessToken, "list_decks", "{}");

        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(["decks"]);
        McpWire.TextOf(result).ShouldContain("\"sharing\":\"private\"", Case.Sensitive);
        var decks = structured.GetProperty("decks").EnumerateArray().ToList();
        decks.Select(d => d.GetProperty("id").GetString()).ShouldBe([second, first]);
        decks.Select(d => d.GetProperty("id").GetString()).ShouldNotContain(alexDeck);
        decks[0].EnumerateObject().Select(p => p.Name).ShouldBe(["id", "slug", "title", "subtitle", "slideCount", "sharing", "url", "updatedLabel"]);
        decks[0].GetProperty("title").GetString().ShouldBe("Second deck");
        decks[0].GetProperty("slideCount").GetInt32().ShouldBe(1);
        decks[0].GetProperty("sharing").GetString().ShouldBe("private");
        decks[0].GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{second}/second-deck");
        decks[0].GetProperty("updatedLabel").GetString().ShouldBe("just now");
        decks[1].GetProperty("updatedLabel").GetString().ShouldBe("10 min ago");
    }

    private static async Task<string> ToolErrorAsync(McpTestClient agent, object args)
    {
        var (isError, json) = await agent.CallToolAsync("get_deck_url", args);
        isError.ShouldBeTrue(json.ToString());
        return json.GetString()!;
    }
}
