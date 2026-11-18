using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Slides;

// SW-454: the refusal of a surface a layout cannot take, proven over the tools and the play view, and what the tool
// descriptions tell an agent about surface and about the two marks notes keep.
[TestFixture]
[CancelAfter(30000)]
public class SlideSurfaceTests : ApiTestFixture
{
    private const string TITLE = """{ "title": "Ship smaller" }""";
    private const string STATEMENT = """{ "statement": "Small batches win." }""";
    private const string CARDS = """{ "headline": "Why", "cards": [{ "title": "One", "text": "First.", "chips": [] }] }""";
    private const string BOLD_NOTES = "**Say:** open with the story.\n- then the numbers\n- then the ask";

    [Test]
    public async Task AddSlide_WhenCardsAreDark_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await ErrorAsync(agent, "add_slide", new { deck, layout = "cards", slots = Slots(CARDS), surface = "dark" });

        error.ShouldBe("cards.surface: this layout is light only");
        (await SlidesAsync(agent, deck)).ShouldBeEmpty();
    }

    [Test]
    public async Task AddSlide_WhenTitleIsLight_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await ErrorAsync(agent, "add_slide", new { deck, layout = "title", slots = Slots(TITLE), surface = "light" });

        error.ShouldBe("title.surface: this layout is dark only");
        (await SlidesAsync(agent, deck)).ShouldBeEmpty();
    }

    [Test]
    public async Task AddSlide_WhenStatementIsDark_ShouldCarryDarkInThePlayView()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        await OkAsync(agent, "add_slide", new { deck, layout = "statement", slots = Slots(STATEMENT), surface = "dark" });

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");
        play.GetProperty("slides")[0].GetProperty("surface").GetString().ShouldBe("dark");
        (await SlidesAsync(agent, deck))[0].GetProperty("surface").GetString().ShouldBe("dark");
    }

    [Test]
    public async Task UpdateSlide_WhenTheLayoutBecomesLightOnlyAndTheSlideIsDark_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await OkAsync(agent, "add_slide", new { deck, layout = "statement", slots = Slots(STATEMENT), surface = "dark" });
        var before = (await SlidesAsync(agent, deck))[0];

        var error = await ErrorAsync(agent, "update_slide", new { deck, slide = 1, revision = 1, slots = Slots(CARDS), layout = "cards" });

        error.ShouldBe("cards.surface: this layout is light only");
        var slide = (await SlidesAsync(agent, deck))[0];
        slide.GetProperty("layout").GetString().ShouldBe("statement");
        slide.GetProperty("surface").GetString().ShouldBe("dark");
        slide.GetProperty("revision").GetInt32().ShouldBe(before.GetProperty("revision").GetInt32());
        slide.GetProperty("slots").GetRawText().ShouldBe(before.GetProperty("slots").GetRawText());
    }

    [Test]
    public async Task UpdateSlide_WhenSurfaceIsNull_ShouldClearIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await OkAsync(agent, "add_slide", new { deck, layout = "statement", slots = Slots(STATEMENT), surface = "dark" });

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "update_slide",
            $$$"""{"deck":"{{{deck}}}","slide":1,"revision":1,"layout":"cards","slots":{{{CARDS}}},"surface":null}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var slide = (await SlidesAsync(agent, deck))[0];
        slide.GetProperty("layout").GetString().ShouldBe("cards");
        slide.GetProperty("surface").GetString().ShouldBe("light");
    }

    [Test]
    public async Task AddSlide_WhenDescribed_ShouldExplainSurface()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var tools = await ToolsAsync(agent);

        foreach (var tool in new[] { "add_slide", "update_slide" })
        {
            var text = tools[tool].GetProperty("inputSchema").GetProperty("properties").GetProperty("surface").GetProperty("description").GetString()!;
            text.ShouldContain("for a layout whose surface is 'either' (list_layouts says which)");
        }

        tools["add_slide"].GetProperty("inputSchema").GetProperty("properties").GetProperty("surface").GetProperty("description").GetString()!
            .ShouldContain("Leave it out for the layout's own surface.");
    }

    [Test]
    public async Task SetNotes_WhenDescribed_ShouldNameTheTwoMarks()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var tools = await ToolsAsync(agent);

        const string marks = "Notes are plain text with two marks: **bold** for cues such as **Say:**, and a line that starts with '- ' for a list item. Nothing else is formatted.";
        tools["set_notes"].GetProperty("description").GetString()!.ShouldContain(marks);
        tools["add_slide"].GetProperty("inputSchema").GetProperty("properties").GetProperty("notes").GetProperty("description").GetString()!.ShouldContain(marks);
    }

    [Test]
    public async Task SetNotes_WhenTextHasBoldMarks_ShouldStoreThemAsGiven()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");

        await OkAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = BOLD_NOTES } } });

        (await SlidesAsync(agent, deck))[0].GetProperty("notes").GetString().ShouldBe(BOLD_NOTES);
    }

    private static JsonElement Slots(string json)
    {
        using var slots = JsonDocument.Parse(json);
        return slots.RootElement.Clone();
    }

    // tools/list by tool name, with each tool's description and input schema.
    private async Task<IReadOnlyDictionary<string, JsonElement>> ToolsAsync(McpTestClient agent)
    {
        var message = await McpWire.SendAsync(Factory, agent.Tokens.AccessToken, """{"jsonrpc":"2.0","id":7,"method":"tools/list","params":{}}""");
        return message.GetProperty("result").GetProperty("tools").EnumerateArray().ToDictionary(tool => tool.GetProperty("name").GetString()!);
    }
}
