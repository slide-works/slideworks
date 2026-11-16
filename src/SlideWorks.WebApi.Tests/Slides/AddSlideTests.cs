using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// K5 and K6: add_slide validates (K2), checks images (K11), inserts at a position with every moved slide re-stamped,
// records activity (K7) and publishes DeckChanged (K9).
[TestFixture]
[CancelAfter(30000)]
public class AddSlideTests : ApiTestFixture
{
    [Test]
    public async Task AddSlide_WhenValid_ShouldReturnNumberStepsRevision()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var first = await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "Why", "items": ["a", "b", "c"], "reveal": "one-by-one" }""");
        var second = await DeckSeed.AddQuoteAsync(agent, deck, "Ship it.");

        first.GetProperty("slide").GetInt32().ShouldBe(1);
        first.GetProperty("steps").GetInt32().ShouldBe(3);
        first.GetProperty("revision").GetInt64().ShouldBe(1);
        first.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller#1");
        first.EnumerateObject().Select(p => p.Name).ShouldBe(["slide", "steps", "revision", "url"], ignoreOrder: true);
        second.GetProperty("slide").GetInt32().ShouldBe(2);
        second.GetProperty("steps").GetInt32().ShouldBe(0);
        second.GetProperty("revision").GetInt64().ShouldBe(2);
    }

    [Test]
    public async Task AddSlide_WhenSlotInvalid_ShouldReturnToolErrorNamingPath()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await AddSlideErrorAsync(agent, deck, "practice", """
            { "headline": "Slice it", "do": [{ "lead": "a", "text": "b" }, { "lead": "c", "text": "d" }, { "text": "e" }],
              "comes_back": [], "avoid": [] }
            """);

        error.ShouldBe("practice.do[2].lead: required");
        (await GetDeckAsync(agent, deck)).GetProperty("slides").GetArrayLength().ShouldBe(0);
    }

    // SlotValidation keeps 20 errors and ends with "…and N more"; the tool error joins them with "; " as they are.
    [Test]
    public async Task AddSlide_WhenMoreThanTwentyErrors_ShouldJoinThemAndSayHowManyMore()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var unknown = Enumerable.Range(1, 25).ToDictionary(n => $"f{n}", _ => "x");
        unknown["quote"] = "Ship it.";
        unknown["attribution"] = "Sam";

        var error = await AddSlideErrorAsync(agent, deck, "quote", JsonSerializer.Serialize(unknown));

        var parts = error.Split("; ");
        parts.Length.ShouldBe(21);
        parts[0].ShouldBe("quote.f1: unknown field");
        parts[^1].ShouldBe("…and 5 more");
    }

    [Test]
    public async Task AddSlide_WhenUnknownLayout_ShouldNameIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await AddSlideErrorAsync(agent, deck, "poster", """{ "title": "Hi" }""");

        error.ShouldStartWith("layout: unknown layout 'poster'");
    }

    // An escaped lone surrogate (half an emoji) can't be read as a .NET string. Sent on the raw wire, because the C#
    // client won't write one. The MCP SDK would fail to read the call's params before any tool runs (JSON-RPC -32603);
    // McpArgumentCheck finds the argument first and the agent gets SlideValidator's text as a tool error.
    [Test]
    public async Task AddSlide_WhenSlotTextHasLoneSurrogate_ShouldBeRefusedWithoutStoringASlide()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var message = await McpWire.SendToolCallAsync(
            Factory,
            agent.Tokens.AccessToken,
            "add_slide",
            $$$"""{"deck":"{{{deck}}}","layout":"quote","slots":{"quote":"Ship it \ud83d","attribution":"Sam"}}""");

        message.TryGetProperty("result", out var result).ShouldBeTrue(message.GetRawText());
        result.GetProperty("isError").GetBoolean().ShouldBeTrue(message.GetRawText());
        McpWire.TextOf(result).ShouldBe("slots: text is not valid UTF-16");
        (await GetDeckAsync(agent, deck)).GetProperty("slides").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_OnTheWire_ShouldSendTheResultAsStructuredContent()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "add_slide",
            $$$"""{"deck":"{{{deck}}}","layout":"quote","slots":{"quote":"Ship it.","attribution":"Sam"}}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.GetProperty("slide").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task AddSlide_WhenOtherPresentersDeck_ShouldSayDeckNotFound()
    {
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var alexDeck = await DeckSeed.CreateDeckAsync(alexAgent);
        await using var samAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var forbidden = await AddSlideErrorAsync(samAgent, alexDeck, "quote", """{ "quote": "Mine now.", "attribution": "Sam" }""");
        var unknown = await AddSlideErrorAsync(samAgent, "nOsUcHdEcK00", "quote", """{ "quote": "Mine now.", "attribution": "Sam" }""");

        forbidden.ShouldBe("Deck not found");
        unknown.ShouldBe("Deck not found");
        (await GetDeckAsync(alexAgent, alexDeck)).GetProperty("slides").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_WhenPositionGiven_ShouldShiftAndRestampLaterSlides()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "A");
        await DeckSeed.AddQuoteAsync(agent, deck, "B");
        await DeckSeed.AddQuoteAsync(agent, deck, "C");

        var added = await DeckSeed.AddQuoteAsync(agent, deck, "D", position: 2);

        added.GetProperty("slide").GetInt32().ShouldBe(2);
        added.GetProperty("revision").GetInt64().ShouldBe(4);
        var slides = (await GetDeckAsync(agent, deck)).GetProperty("slides").EnumerateArray().ToList();
        slides.Select(s => s.GetProperty("number").GetInt32()).ShouldBe([1, 2, 3, 4]);
        slides.Select(s => s.GetProperty("slots").GetProperty("quote").GetString()).ShouldBe(["A", "D", "B", "C"]);
        slides.Select(s => s.GetProperty("revision").GetInt64()).ShouldBe([1, 4, 4, 4]);
    }

    [TestCase(0)]
    [TestCase(3)]
    public async Task AddSlide_WhenPositionOutOfRange_ShouldSayTheRange(int position)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "A");

        var error = await AddSlideErrorAsync(agent, deck, "quote", """{ "quote": "B", "attribution": "Sam" }""", position: position);

        error.ShouldBe("position: must be between 1 and 2");
    }

    [Test]
    public async Task AddSlide_WhenPartOutOfRange_ShouldSayTheRange()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: ["One", "Two"]);

        var error = await AddSlideErrorAsync(agent, deck, "quote", """{ "quote": "B", "attribution": "Sam" }""", part: 3);

        error.ShouldBe("part: must be between 1 and 2");
    }

    [Test]
    public async Task AddSlide_WhenImageNotUploaded_ShouldNameImage()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var image = await AddSlideErrorAsync(agent, deck, "image", """
            { "headline": "The board", "image": { "image": "img_3f9", "alt": "A taskboard" } }
            """);
        var twoColumn = await AddSlideErrorAsync(agent, deck, "two-column", """
            { "headline": "Before and after", "left": { "text": "Before" }, "right": { "image": { "image": "img_after01", "alt": "After" } } }
            """);

        image.ShouldBe("image.image: unknown image img_3f9");
        twoColumn.ShouldBe("two-column.right.image: unknown image img_after01");
    }

    // The validator treats a null optional slot as absent, and the client's slot types have no null, so it is not stored.
    [Test]
    public async Task AddSlide_WhenOptionalSlotIsNull_ShouldStoreItAsAbsent()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """
            { "kicker": null, "headline": "Why", "items": ["a", { "lead": "b", "text": "c" }], "callout": null }
            """);

        var slots = (await GetDeckAsync(agent, deck)).GetProperty("slides")[0].GetProperty("slots");
        slots.EnumerateObject().Select(p => p.Name).ShouldBe(["headline", "items"]);
        slots.GetProperty("items")[1].GetProperty("lead").GetString().ShouldBe("b");
    }

    [Test]
    public async Task AddSlide_ShouldRecordAgentActivity()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "A");

        await DeckSeed.AddSlideAsync(agent, deck, "practice", """
            { "headline": "Slice it", "do": [{ "lead": "a", "text": "b" }], "comes_back": [], "avoid": [] }
            """, position: 1);

        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity.Select(e => e.GetProperty("summary").GetString()).ShouldBe(["added slide 1 (practice)", "added slide 1 (quote)", "created the deck"]);
        activity[0].GetProperty("tool").GetString().ShouldBe("add_slide");
        activity[0].GetProperty("actor").GetString().ShouldBe("Claude");
        activity[0].GetProperty("actorKind").GetString().ShouldBe("agent");
        activity[0].GetProperty("via").GetString().ShouldBe("mcp");
    }

    [Test]
    public async Task AddSlide_ShouldPublishDeckChanged()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        await DeckSeed.AddQuoteAsync(agent, deck, "A");

        var deckEvent = await received.Reader.ReadAsync();
        deckEvent.ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
    }

    private static async Task<string> AddSlideErrorAsync(McpTestClient agent, string deck, string layout, string slotsJson, int? part = null, int? position = null)
    {
        using var slots = JsonDocument.Parse(slotsJson);
        var (isError, json) = await agent.CallToolAsync("add_slide", new { deck, layout, slots = slots.RootElement, part, position });
        isError.ShouldBeTrue(json.ToString());
        return json.GetString()!;
    }

    private static async Task<JsonElement> GetDeckAsync(McpTestClient agent, string deck)
    {
        var (isError, json) = await agent.CallToolAsync("get_deck", new { deck });
        isError.ShouldBeFalse(json.ToString());
        return json;
    }
}
