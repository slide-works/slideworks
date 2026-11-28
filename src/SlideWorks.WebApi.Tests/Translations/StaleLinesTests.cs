using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K4 and D13: update_slide re-keys a slide's stored lines and warns about lines that are out of date or were removed,
// while translations are on; add_slide and add_slides leave the other slides' lines where they are.
[TestFixture]
[CancelAfter(60000)]
public class StaleLinesTests : ApiTestFixture
{
    private const string PLAN = """{ "headline": "Plan", "items": ["A", "B"] }""";

    [Test]
    public async Task UpdateSlide_WhenPrimaryTextChanged_ShouldWarnStaleLines()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", PLAN);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(deck, true, "en", "th", "ja");
        await seed.LinesAsync(deck, 1, new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = Entries(("/headline", "แผน", "Plan"), ("/items/0", "เอ", "A")),
            ["ja"] = Entries(("/headline", "計画", "Plan")),
        });

        var updated = await SlideCalls.UpdateAsync(agent, deck, 1, 1, """{ "headline": "New plan", "items": ["A2", "B"] }""");

        Warnings(updated).ShouldBe(["slide 1: 2 Thai lines and 1 Japanese line are out of date (/headline, /items/0); translate them again with translate_slides"]);
        States(await LinesAsync(agent, deck, "th")).ShouldBe([("/headline", "stale"), ("/items/0", "stale"), ("/items/1", "missing")]);
    }

    [Test]
    public async Task UpdateSlide_WhenItemMoved_ShouldRekeyTranslation()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", PLAN);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(deck, true, "en", "th");
        await seed.LinesAsync(deck, 1, Thai(("/items/0", "เอ", "A")));

        var updated = await SlideCalls.UpdateAsync(agent, deck, 1, 1, """{ "headline": "Plan", "items": ["B", "A"] }""");

        Warnings(updated).ShouldBeEmpty();
        var moved = (await LinesAsync(agent, deck, "th")).Single(line => line.GetProperty("path").GetString() == "/items/1");
        moved.GetProperty("translation").GetString().ShouldBe("เอ");
        moved.GetProperty("state").GetString().ShouldBe("current");
        States(await LinesAsync(agent, deck, "th")).ShouldBe([("/headline", "missing"), ("/items/0", "missing"), ("/items/1", "current")]);
    }

    [Test]
    public async Task UpdateSlide_WhenItemDeleted_ShouldWarnRemoved()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", PLAN);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(deck, true, "en", "th");
        await seed.LinesAsync(deck, 1, Thai(("/headline", "แผน", "Plan"), ("/items/1", "บี", "B")));

        var updated = await SlideCalls.UpdateAsync(agent, deck, 1, 1, """{ "headline": "Plan", "items": ["A"] }""");

        Warnings(updated).ShouldBe(["slide 1: 1 Thai line no longer exists and was removed"]);
        States(await LinesAsync(agent, deck, "th")).ShouldBe([("/headline", "current"), ("/items/0", "missing")]);
    }

    [Test]
    public async Task UpdateSlide_WhenTranslationsOff_ShouldNotWarn()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", PLAN);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(deck, false, "en", "th");
        await seed.LinesAsync(deck, 1, Thai(("/headline", "แผน", "Plan"), ("/items/0", "เอ", "A")));

        var updated = await SlideCalls.UpdateAsync(agent, deck, 1, 1, """{ "headline": "New plan", "items": ["B"] }""");
        await seed.SettingsAsync(deck, true, "en", "th");

        Warnings(updated).ShouldBeEmpty();
        // Nothing was re-keyed while off: the entry for "A" is still at /items/0, now stale against "B".
        States(await LinesAsync(agent, deck, "th")).ShouldBe([("/headline", "stale"), ("/items/0", "stale")]);
    }

    [Test]
    public async Task AddSlides_WhenInsertedBefore_ShouldKeepLaterSlidesTranslations()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", PLAN);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(deck, true, "en", "th");
        await seed.LinesAsync(deck, 1, Thai(("/headline", "แผน", "Plan")));

        var added = await SlideCalls.OkAsync(agent, "add_slides", new
        {
            deck,
            position = 1,
            slides = new object[]
            {
                new { layout = "quote", slots = new { quote = "Yes", attribution = "Sam" } },
                new { layout = "quote", slots = new { quote = "No", attribution = "Sam" } },
            },
        });
        var one = await DeckSeed.AddQuoteAsync(agent, deck, "Maybe", position: 1);

        added.GetProperty("slides").EnumerateArray().ShouldAllBe(slide => slide.GetProperty("warnings").GetArrayLength() == 0);
        one.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        var deckRead = await SlideCalls.OkAsync(agent, "get_deck", new { deck, language = "th" });
        var plan = deckRead.GetProperty("slides")[3];
        plan.GetProperty("layout").GetString().ShouldBe("bullets");
        States(plan.GetProperty("lines").EnumerateArray().ToList()).ShouldBe([("/headline", "current"), ("/items/0", "missing"), ("/items/1", "missing")]);
        deckRead.GetProperty("slides")[0].GetProperty("lines").EnumerateArray().Select(line => line.GetProperty("state").GetString()).ShouldBe(["missing", "missing"]);
    }

    private static Dictionary<string, (string Text, string From)> Entries(params (string Path, string Text, string From)[] lines)
        => lines.ToDictionary(line => line.Path, line => (line.Text, line.From));

    private static Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>> Thai(params (string Path, string Text, string From)[] lines)
        => new() { ["th"] = Entries(lines) };

    private static async Task<IReadOnlyList<JsonElement>> LinesAsync(McpTestClient agent, string deck, string language)
        => (await SlideCalls.OkAsync(agent, "get_deck", new { deck, language })).GetProperty("slides")[0].GetProperty("lines").EnumerateArray().ToList();

    private static IEnumerable<(string?, string?)> States(IEnumerable<JsonElement> lines)
        => lines.Select(line => (line.GetProperty("path").GetString(), line.GetProperty("state").GetString()));

    private static IEnumerable<string?> Warnings(JsonElement result)
        => result.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString());
}
