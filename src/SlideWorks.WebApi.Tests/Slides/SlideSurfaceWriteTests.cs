using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Slides;

// K6, the write side of a slide's surface: add_slide and update_slide take "light" or "dark" and store it; update_slide
// keeps the stored one when surface is left out and clears it on a JSON null. The surface the slide will have is checked
// against its layout, so a layout change that the stored surface does not fit is refused, never silently cleared. No
// read gives the surface back yet, so these tests read the stored column.
[TestFixture]
[CancelAfter(30000)]
public class SlideSurfaceWriteTests : ApiTestFixture
{
    private const string TITLE = """{ "title": "Ship smaller" }""";
    private const string QUOTE = """{ "quote": "Ship it.", "attribution": "Sam Rivera" }""";
    private const string TABLE = """{ "headline": "Ship smaller", "columns": ["Was", "Now"], "rows": [["Big", "Small"]] }""";
    private const string NOT_A_WORD = "surface: use 'light' or 'dark'";

    [Test]
    public async Task AddSlide_WhenSurfaceIsDark_ShouldStoreIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var added = await AddAsync(agent, deck, "title", TITLE, "dark");
        await DeckSeed.AddQuoteAsync(agent, deck, "No surface given.");

        added.GetProperty("slide").GetInt32().ShouldBe(1);
        (await StoredSurfacesAsync(deck)).ShouldBe(["dark", null]);
    }

    [Test]
    public async Task AddSlide_OnTheWireWithASurface_ShouldStoreIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "add_slide",
            $$$"""{"deck":"{{{deck}}}","layout":"title","slots":{"title":"Ship smaller"},"surface":"dark"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("structuredContent").GetProperty("slide").GetInt32().ShouldBe(1);
        (await StoredSurfacesAsync(deck)).ShouldBe(["dark"]);
    }

    [TestCase("\"blue\"", NOT_A_WORD)]
    [TestCase("\"Dark\"", NOT_A_WORD)]
    [TestCase("\"\"", NOT_A_WORD)]
    [TestCase("5", NOT_A_WORD)]
    [TestCase("{\"surface\":\"dark\"}", NOT_A_WORD)]
    [TestCase("\"light\"", "title.surface: this layout is dark only")]
    public async Task AddSlide_WhenSurfaceIsNotAWord_ShouldRefuse(string surfaceJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Already here.");
        var before = await DeckTextAsync(agent, deck);

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "add_slide",
            $$$"""{"deck":"{{{deck}}}","layout":"title","slots":{"title":"Ship smaller"},"position":1,"surface":{{{surfaceJson}}}}""");

        result.GetProperty("isError").GetBoolean().ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldBe(expected);
        (await DeckTextAsync(agent, deck)).ShouldBe(before);
        (await StoredSurfacesAsync(deck)).ShouldBe([null]);
    }

    [Test]
    public async Task UpdateSlide_WhenSurfaceIsLeftOut_ShouldKeepIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await AddAsync(agent, deck, "title", TITLE, "dark");

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "update_slide",
            $$$"""{"deck":"{{{deck}}}","slide":1,"revision":1,"slots":{"title":"Ship even smaller"}}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("structuredContent").GetProperty("revision").GetInt64().ShouldBe(2);
        (await StoredSurfacesAsync(deck)).ShouldBe(["dark"]);
    }

    [Test]
    public async Task UpdateSlide_WhenSurfaceIsNull_ShouldClearIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await AddAsync(agent, deck, "title", TITLE, "dark");

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "update_slide",
            $$$"""{"deck":"{{{deck}}}","slide":1,"revision":1,"slots":{"title":"Ship smaller"},"surface":null}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("structuredContent").ValueKind.ShouldBe(JsonValueKind.Object);
        (await StoredSurfacesAsync(deck)).ShouldBe([null]);
    }

    [Test]
    public async Task UpdateSlide_WhenSurfaceIsAWord_ShouldStoreIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "title", TITLE);

        var updated = await OkAsync(agent, "update_slide", new { deck, slide = 1, revision = 1, slots = Slots(TITLE), surface = "dark" });

        updated.GetProperty("revision").GetInt64().ShouldBe(2);
        (await StoredSurfacesAsync(deck)).ShouldBe(["dark"]);
    }

    [TestCase("\"blue\"", NOT_A_WORD)]
    [TestCase("5", NOT_A_WORD)]
    [TestCase("true", NOT_A_WORD)]
    [TestCase("{\"surface\":\"dark\"}", NOT_A_WORD)]
    [TestCase("\"light\"", "title.surface: this layout is dark only")]
    public async Task UpdateSlide_WhenSurfaceIsNotAWord_ShouldRefuse(string surfaceJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await AddAsync(agent, deck, "title", TITLE, "dark");
        var before = await DeckTextAsync(agent, deck);

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "update_slide",
            $$$"""{"deck":"{{{deck}}}","slide":1,"revision":1,"slots":{"title":"Changed"},"surface":{{{surfaceJson}}}}""");

        result.GetProperty("isError").GetBoolean().ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldBe(expected);
        (await DeckTextAsync(agent, deck)).ShouldBe(before);
        (await StoredSurfacesAsync(deck)).ShouldBe(["dark"]);
    }

    // D75: the stored surface is checked against the new layout, and only the caller clears it.
    [Test]
    public async Task UpdateSlide_WhenTheNewLayoutCannotTakeTheStoredSurface_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await AddAsync(agent, deck, "title", TITLE, "dark");
        var before = await DeckTextAsync(agent, deck);

        var error = await ErrorAsync(agent, "update_slide", new { deck, slide = 1, revision = 1, slots = Slots(TABLE), layout = "table" });

        error.ShouldBe("table.surface: this layout is light only");
        (await DeckTextAsync(agent, deck)).ShouldBe(before);
        (await StoredSurfacesAsync(deck)).ShouldBe(["dark"]);
    }

    [Test]
    public async Task UpdateSlide_WhenTheLayoutChangesAndSurfaceIsNull_ShouldChangeItAndClearTheSurface()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await AddAsync(agent, deck, "title", TITLE, "dark");

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "update_slide",
            $$$"""{"deck":"{{{deck}}}","slide":1,"revision":1,"layout":"quote","slots":{{{QUOTE}}},"surface":null}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        (await SlidesAsync(agent, deck))[0].GetProperty("layout").GetString().ShouldBe("quote");
        (await StoredSurfacesAsync(deck)).ShouldBe([null]);
    }

    [Test]
    public async Task ToolsList_WhenRead_ShouldDescribeSurfaceOnBothWriteTools()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var schemas = await McpWire.InputSchemasAsync(Factory, agent.Tokens.AccessToken);

        var add = schemas["add_slide"];
        var update = schemas["update_slide"];
        add.GetProperty("properties").TryGetProperty("surface", out _).ShouldBeTrue(add.GetRawText());
        update.GetProperty("properties").TryGetProperty("surface", out _).ShouldBeTrue(update.GetRawText());
        add.GetProperty("required").EnumerateArray().Select(r => r.GetString()).ShouldNotContain("surface");
        update.GetProperty("required").EnumerateArray().Select(r => r.GetString()).ShouldNotContain("surface");
    }

    private static async Task<JsonElement> AddAsync(McpTestClient agent, string deck, string layout, string slotsJson, string surface)
    {
        var (isError, json) = await agent.CallToolAsync("add_slide", new { deck, layout, slots = Slots(slotsJson), surface });
        isError.ShouldBeFalse(json.ToString());
        return json;
    }

    private static JsonElement Slots(string json)
    {
        using var slots = JsonDocument.Parse(json);
        return slots.RootElement.Clone();
    }

    // get_deck as text: the deck's and every slide's revision, the slides and their slots.
    private static async Task<string> DeckTextAsync(McpTestClient agent, string deck)
        => (await OkAsync(agent, "get_deck", new { deck })).GetRawText();

    // The stored surface of each live slide, in order.
    // TODO: read this through get_deck once slide reads carry the surface.
    private async Task<IReadOnlyList<string?>> StoredSurfacesAsync(string deck)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Slides.AsNoTracking().Where(s => s.DeckId == deck && s.DeletedAt == null).OrderBy(s => s.Position).Select(s => s.Surface).ToListAsync();
    }
}
