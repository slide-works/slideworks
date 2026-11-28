using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K4: get_deck with a language lists each slide's lines with their state, carries the translations summary while
// translations are on, keeps today's keys while they are off, and refuses a language the deck doesn't have.
[TestFixture]
[CancelAfter(60000)]
public class GetDeckLinesTests : ApiTestFixture
{
    private const string BULLETS = """
        { "headline": "Small steps win", "items": ["Ship daily", "Merge often"], "emphasis": [{ "text": "Small steps", "tone": "accent" }] }
        """;

    private static readonly string[] _deckKeys = ["id", "slug", "title", "subtitle", "author", "parts", "sharing", "url", "look", "talk_minutes", "slides", "qa", "reactions"];

    [Test]
    public async Task GetDeck_WhenLanguageGiven_ShouldListEveryLineWithState()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(deck, true, "en", "th");
        await seed.LinesAsync(deck, 1, Thai(("/headline", "ก้าวเล็กชนะ", "Small steps win"), ("/items/0", "ส่งทุกสัปดาห์", "Ship weekly")));

        var full = await SlideCalls.OkAsync(agent, "get_deck", new { deck, language = "th" });
        var summary = await SlideCalls.OkAsync(agent, "get_deck", new { deck, detail = "summary", language = "th" });

        (string?, string?, string?, string?)[] expected =
        [
            ("/headline", "Small steps win", "ก้าวเล็กชนะ", "current"),
            ("/items/0", "Ship daily", "ส่งทุกสัปดาห์", "stale"),
            ("/items/1", "Merge often", null, "missing"),
            ("/emphasis/0/text", "Small steps", null, "missing"),
        ];
        var fullSlide = full.GetProperty("slides")[0];
        Lines(fullSlide).ShouldBe(expected);
        fullSlide.GetProperty("lines")[0].EnumerateObject().Select(p => p.Name).ShouldBe(["path", "primary", "translation", "state"]);
        fullSlide.GetProperty("slots").GetProperty("headline").GetString().ShouldBe("Small steps win");
        Lines(summary.GetProperty("slides")[0]).ShouldBe(expected);
        var translations = full.GetProperty("translations");
        translations.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        translations.GetProperty("primary").GetString().ShouldBe("en");
        var thai = translations.GetProperty("languages").EnumerateArray().ShouldHaveSingleItem();
        (thai.GetProperty("code").GetString(), thai.GetProperty("lines").GetInt32(), thai.GetProperty("current").GetInt32(), thai.GetProperty("stale").GetInt32(), thai.GetProperty("missing").GetInt32())
            .ShouldBe(("th", 4, 1, 1, 2));
        summary.GetProperty("translations").GetRawText().ShouldBe(translations.GetRawText());
    }

    [Test]
    public async Task GetDeck_WhenTranslationsOff_ShouldMatchTodaysKeys()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(deck, false, "en", "th");
        await seed.LinesAsync(deck, 1, Thai(("/headline", "ก้าวเล็กชนะ", "Small steps win")));

        var full = await SlideCalls.OkAsync(agent, "get_deck", new { deck });
        var summary = await SlideCalls.OkAsync(agent, "get_deck", new { deck, detail = "summary" });
        var primary = await SlideCalls.OkAsync(agent, "get_deck", new { deck, language = "en" });

        full.EnumerateObject().Select(p => p.Name).ShouldBe(_deckKeys);
        summary.EnumerateObject().Select(p => p.Name).ShouldBe(_deckKeys);
        primary.EnumerateObject().Select(p => p.Name).ShouldBe(_deckKeys);
        full.GetProperty("slides")[0].EnumerateObject().Select(p => p.Name).ShouldBe(["number", "layout", "part", "deep_dive", "surface", "steps", "revision", "slots", "notes"]);
        summary.GetProperty("slides")[0].EnumerateObject().Select(p => p.Name).ShouldBe(["number", "layout", "part", "headline"]);
        primary.GetProperty("slides").GetRawText().ShouldBe(full.GetProperty("slides").GetRawText());
    }

    [Test]
    public async Task GetDeck_WhenLanguageNotInDeck_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        await new TranslationRowSeed(Factory.Services).SettingsAsync(deck, true, "en", "ja");

        var error = await SlideCalls.ErrorAsync(agent, "get_deck", new { deck, language = "th" });

        error.ShouldBe("th is not one of this deck's languages (en, ja)");
    }

    private static Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>> Thai(params (string Path, string Text, string From)[] lines)
        => new() { ["th"] = lines.ToDictionary(line => line.Path, line => (line.Text, line.From)) };

    private static IEnumerable<(string?, string?, string?, string?)> Lines(JsonElement slide)
        => slide.GetProperty("lines").EnumerateArray().Select(line => (
            line.GetProperty("path").GetString(),
            line.GetProperty("primary").GetString(),
            line.GetProperty("translation").GetString(),
            line.GetProperty("state").GetString()));
}
