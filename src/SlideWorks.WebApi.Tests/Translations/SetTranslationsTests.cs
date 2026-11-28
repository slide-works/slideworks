using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K4 set_translations: the result, the refusals (which change nothing) and what stays when translations go off.
[TestFixture]
[CancelAfter(60000)]
public class SetTranslationsTests : ApiTestFixture
{
    private const string BULLETS = """
        { "headline": "Small steps win", "items": ["Ship daily", "Merge often"] }
        """;

    [Test]
    public async Task SetTranslations_WhenTurnedOn_ShouldReturnCoverageForEachLanguage()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.LinesAsync(deck, 1, Thai(("/headline", "ก้าวเล็กชนะ", "Small steps win"), ("/items/0", "ส่งทุกสัปดาห์", "Ship weekly")));

        var result = await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = true, primary = "en", languages = new[] { "th", "ja" } });

        result.EnumerateObject().Select(p => p.Name).ShouldBe(["text", "translations"]);
        var translations = result.GetProperty("translations");
        translations.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        translations.GetProperty("primary").GetString().ShouldBe("en");
        translations.GetProperty("languages").EnumerateArray()
            .Select(l => (l.GetProperty("code").GetString(), l.GetProperty("lines").GetInt32(), l.GetProperty("current").GetInt32(), l.GetProperty("stale").GetInt32(), l.GetProperty("missing").GetInt32()))
            .ShouldBe([("th", 3, 1, 1, 1), ("ja", 3, 0, 0, 3)]);
        var got = await SlideCalls.OkAsync(agent, "get_deck", new { deck });
        got.GetProperty("translations").GetRawText().ShouldBe(translations.GetRawText());
    }

    [Test]
    public async Task SetTranslations_WhenNothingGiven_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await SlideCalls.ErrorAsync(agent, "set_translations", new { deck });

        error.ShouldBe("give at least one of enabled, primary or languages");
    }

    [Test]
    public async Task SetTranslations_WhenUnsupported_ShouldNameSupportedCodes()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await SlideCalls.ErrorAsync(agent, "set_translations", new { deck, enabled = true, languages = new[] { "th", "xx" } });

        error.ShouldBe("xx is not a supported language; use one of: en, es, fr, de, it, pt, nl, id, ms, vi, th, ja, ko, zh-Hans, zh-Hant");
        (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).TryGetProperty("translations", out _).ShouldBeFalse();
    }

    [Test]
    public async Task SetTranslations_WhenRightToLeft_ShouldRefuseAndChangeNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var revision = await RevisionAsync(deck);

        var error = await SlideCalls.ErrorAsync(agent, "set_translations", new { deck, enabled = true, languages = new[] { "ar" } });

        error.ShouldBe("right-to-left languages are not supported yet: ar");
        (await RevisionAsync(deck)).ShouldBe(revision);
        (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).TryGetProperty("translations", out _).ShouldBeFalse();
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task SetTranslations_WhenPrimaryAlsoTranslation_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await SlideCalls.ErrorAsync(agent, "set_translations", new { deck, enabled = true, primary = "th", languages = new[] { "th" } });

        error.ShouldBe("th can't be both the primary language and a translation");
        (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).TryGetProperty("translations", out _).ShouldBeFalse();
    }

    [Test]
    public async Task SetTranslations_WhenTurnedOff_ShouldKeepStoredLines()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.LinesAsync(deck, 1, Thai(("/headline", "ก้าวเล็กชนะ", "Small steps win")));
        await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = true, languages = new[] { "th" } });

        var off = await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = false });
        var on = await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = true });

        off.GetProperty("translations").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        var thai = on.GetProperty("translations").GetProperty("languages").EnumerateArray().ShouldHaveSingleItem();
        (thai.GetProperty("code").GetString(), thai.GetProperty("current").GetInt32(), thai.GetProperty("missing").GetInt32()).ShouldBe(("th", 1, 2));
    }

    [Test]
    public async Task SetTranslations_WhenChanged_ShouldBumpRevision()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var before = await RevisionAsync(deck);

        await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = true, languages = new[] { "th" } });

        (await RevisionAsync(deck)).ShouldBe(before + 1);
    }

    private async Task<long> RevisionAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.AsNoTracking().SingleAsync(d => d.Id == deck)).Revision;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>> Thai(params (string Path, string Text, string From)[] lines)
        => new() { ["th"] = lines.ToDictionary(l => l.Path, l => (l.Text, l.From)) };
}
