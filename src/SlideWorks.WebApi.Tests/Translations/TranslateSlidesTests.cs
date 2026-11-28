using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K4: translate_slides stores one language's lines across slides, all or nothing.
[TestFixture]
[CancelAfter(60000)]
public class TranslateSlidesTests : ApiTestFixture
{
    private const string QUOTE = "Small batches ship sooner";
    private const string NAME = "Sam Rivera";

    [Test]
    public async Task TranslateSlides_WhenLinesGiven_ShouldStoreWithFrom()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await QuoteDeckAsync(agent, 1, "th");

        var (isError, json) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "th",
            slides = new[] { new { slide = 1, revision = await RevisionOfSlideAsync(agent, deck, 1), lines = new Dictionary<string, string?> { ["/quote"] = "ชุดเล็กส่งได้เร็วกว่า", ["/attribution"] = "แซม" } } },
        });

        isError.ShouldBeFalse(json.ToString());
        json.GetProperty("language").GetString().ShouldBe("th");
        json.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        var slide = json.GetProperty("slides").EnumerateArray().ShouldHaveSingleItem();
        slide.GetProperty("slide").GetInt32().ShouldBe(1);
        slide.GetProperty("revision").GetInt64().ShouldBe(json.GetProperty("revision").GetInt64());
        slide.GetProperty("lines").GetProperty("/quote").GetString().ShouldBe("ชุดเล็กส่งได้เร็วกว่า");
        slide.GetProperty("current").GetInt32().ShouldBe(2);
        slide.GetProperty("stale").GetInt32().ShouldBe(0);
        slide.GetProperty("missing").GetInt32().ShouldBe(0);
        using var stored = JsonDocument.Parse((await StoredLinesAsync(deck, 1))!);
        var thai = stored.RootElement.GetProperty("th");
        thai.GetProperty("/quote").GetProperty("text").GetString().ShouldBe("ชุดเล็กส่งได้เร็วกว่า");
        thai.GetProperty("/quote").GetProperty("from").GetString().ShouldBe(QUOTE);
        thai.GetProperty("/attribution").GetProperty("text").GetString().ShouldBe("แซม");
        thai.GetProperty("/attribution").GetProperty("from").GetString().ShouldBe(NAME);
    }

    [Test]
    public async Task TranslateSlides_WhenOneSlideStale_ShouldRefuseAllAndStoreNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await QuoteDeckAsync(agent, 2, "th");
        var first = await RevisionOfSlideAsync(agent, deck, 1);
        var stale = await RevisionOfSlideAsync(agent, deck, 2) - 1;

        var (isError, json) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "th",
            slides = new[]
            {
                new { slide = 1, revision = first, lines = new Dictionary<string, string?> { ["/quote"] = "ชุดเล็ก" } },
                new { slide = 2, revision = stale, lines = new Dictionary<string, string?> { ["/quote"] = "ชุดเล็ก" } },
            },
        });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe($"Slide 2 changed since revision {stale}; read it again with get_deck");
        (await StoredLinesAsync(deck, 1)).ShouldBeNull();
        (await StoredLinesAsync(deck, 2)).ShouldBeNull();
        (await RevisionOfSlideAsync(agent, deck, 1)).ShouldBe(first);
    }

    [Test]
    public async Task TranslateSlides_WhenPathNotALine_ShouldNameSlideAndPath()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await QuoteDeckAsync(agent, 3, "th");

        var (isError, json) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "th",
            slides = new[]
            {
                new { slide = 1, revision = await RevisionOfSlideAsync(agent, deck, 1), lines = new Dictionary<string, string?> { ["/quote"] = "ชุดเล็ก" } },
                new { slide = 3, revision = await RevisionOfSlideAsync(agent, deck, 3), lines = new Dictionary<string, string?> { ["/foo"] = "ชุดเล็ก" } },
            },
        });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("slide 3: /foo is not a line on this slide; read the lines with get_deck and language th");
        (await StoredLinesAsync(deck, 1)).ShouldBeNull();
    }

    [Test]
    public async Task TranslateSlides_WhenEmphasisMissingFromTranslation_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{"headline":"Ship small batches","emphasis":[{"text":"small","tone":"accent"}],"items":["Review sooner"]}""");
        await new TranslationRowSeed(Factory.Services).SettingsAsync(deck, true, "en", "th");

        var (isError, json) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "th",
            slides = new[] { new { slide = 1, revision = await RevisionOfSlideAsync(agent, deck, 1), lines = new Dictionary<string, string?> { ["/headline"] = "ส่งงานเป็นชุดเล็ก" } } },
        });

        isError.ShouldBeTrue();
        json.GetString()!.ShouldStartWith("slide 1 in Thai: ", Case.Sensitive);
        json.GetString()!.ShouldContain("small", Case.Sensitive);
        (await StoredLinesAsync(deck, 1)).ShouldBeNull();
    }

    [Test]
    public async Task TranslateSlides_WhenTranslationsOff_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await QuoteDeckAsync(agent, 1, "th");
        await new TranslationRowSeed(Factory.Services).SettingsAsync(deck, false, "en", "th");

        var (isError, json) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "th",
            slides = new[] { new { slide = 1, revision = await RevisionOfSlideAsync(agent, deck, 1), lines = new Dictionary<string, string?> { ["/quote"] = "ชุดเล็ก" } } },
        });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("turn translations on with set_translations and add th first");
        (await StoredLinesAsync(deck, 1)).ShouldBeNull();
    }

    [Test]
    public async Task TranslateSlides_WhenPrimaryLanguage_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await QuoteDeckAsync(agent, 1, "th");

        var (isError, json) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "en",
            slides = new[] { new { slide = 1, revision = await RevisionOfSlideAsync(agent, deck, 1), lines = new Dictionary<string, string?> { ["/quote"] = "Small ships" } } },
        });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("en is the primary language; change the slides with update_slide instead");
        (await StoredLinesAsync(deck, 1)).ShouldBeNull();
    }

    [Test]
    public async Task TranslateSlides_WhenDone_ShouldRecordActivityAndBumpRevision()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await QuoteDeckAsync(agent, 3, "th");
        // The last slide added carries the deck's revision: appending restamps no other slide.
        var third = await RevisionOfSlideAsync(agent, deck, 3);
        var before = third;

        var (isError, json) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "th",
            slides = new[]
            {
                new { slide = 1, revision = await RevisionOfSlideAsync(agent, deck, 1), lines = new Dictionary<string, string?> { ["/quote"] = "ชุดเล็ก" } },
                new { slide = 2, revision = await RevisionOfSlideAsync(agent, deck, 2), lines = new Dictionary<string, string?> { ["/quote"] = "ชุดเล็ก" } },
            },
        });
        isError.ShouldBeFalse(json.ToString());
        var (removeError, removed) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "th",
            slides = new[] { new { slide = 2, revision = await RevisionOfSlideAsync(agent, deck, 2), lines = new Dictionary<string, string?> { ["/quote"] = null } } },
        });

        removeError.ShouldBeFalse(removed.ToString());
        json.GetProperty("revision").GetInt64().ShouldBe(before + 1);
        removed.GetProperty("revision").GetInt64().ShouldBe(before + 2);
        (await RevisionOfSlideAsync(agent, deck, 1)).ShouldBe(before + 1);
        (await RevisionOfSlideAsync(agent, deck, 2)).ShouldBe(before + 2);
        (await RevisionOfSlideAsync(agent, deck, 3)).ShouldBe(third);
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity.Take(2).Select(e => e.GetProperty("summary").GetString()).ShouldBe(["removed 1 Thai line on slide 2", "translated 2 slides into Thai"]);
        activity[0].GetProperty("tool").GetString().ShouldBe("translate_slides");
        activity[0].GetProperty("actor").GetString().ShouldBe("Claude");
    }

    // A deck of quote slides with translations on, primary en, in these languages.
    private async Task<string> QuoteDeckAsync(McpTestClient agent, int slides, params string[] languages)
    {
        var deck = await DeckSeed.CreateDeckAsync(agent);
        for (var index = 0; index < slides; index++)
        {
            await DeckSeed.AddQuoteAsync(agent, deck, QUOTE);
        }
        await new TranslationRowSeed(Factory.Services).SettingsAsync(deck, true, "en", languages);
        return deck;
    }

    private static async Task<long> RevisionOfSlideAsync(McpTestClient agent, string deck, int slide)
    {
        var (isError, json) = await agent.CallToolAsync("get_deck", new { deck });
        isError.ShouldBeFalse(json.ToString());
        return json.GetProperty("slides").EnumerateArray().Single(s => s.GetProperty("number").GetInt32() == slide).GetProperty("revision").GetInt64();
    }

    // The raw TranslationsJson of the deck's live slide with this 1-based number.
    private async Task<string?> StoredLinesAsync(string deck, int slide)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Slides.AsNoTracking().Where(s => s.DeckId == deck && s.DeletedAt == null && s.Position == slide - 1).Select(s => s.TranslationsJson).SingleAsync();
    }
}
