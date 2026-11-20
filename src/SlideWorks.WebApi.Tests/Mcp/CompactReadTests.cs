using System.Text;
using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

// list_layouts {compact} and get_deck {summary}: shorter reads for an agent, with today's bodies unchanged when the flag is left out.
[TestFixture]
public class CompactReadTests : ApiTestFixture
{
    [Test]
    public async Task ListLayouts_WhenCompact_ShouldNameTheIconsOnceAndStayUnderHalfTheFullBody()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var full = await SlideCalls.OkAsync(agent, "list_layouts", new { });
        var compact = await SlideCalls.OkAsync(agent, "list_layouts", new { compact = true });

        Encoding.UTF8.GetByteCount(compact.GetRawText()).ShouldBeLessThan(Encoding.UTF8.GetByteCount(full.GetRawText()) / 2);
        compact.GetProperty("icons").EnumerateArray().Select(i => i.GetString()).ShouldBe(IconCatalogue.Names);
        var blockNames = compact.GetProperty("blocks").EnumerateObject().Select(b => b.Name).ToList();
        InlineBlocks.All.Select(b => b.Name).ShouldAllBe(name => blockNames.Contains(name));
        blockNames.ShouldContain("emphasis");
        compact.GetProperty("blocks").EnumerateObject().ShouldAllBe(b => b.Value.GetProperty("slots").ValueKind == JsonValueKind.Array);
        var layouts = compact.GetProperty("layouts").EnumerateArray().ToList();
        layouts.Select(l => l.GetProperty("name").GetString()).ShouldBe(LayoutCatalogue.Layouts.Select(l => l.Name));
        var slots = layouts.SelectMany(l => l.GetProperty("slots").EnumerateArray()).ToList();
        slots.ShouldAllBe(s => s.GetProperty("required").ValueKind == JsonValueKind.True || s.GetProperty("required").ValueKind == JsonValueKind.False);
        slots.All(s => s.TryGetProperty("max", out _)).ShouldBeTrue();
        compact.GetRawText().ShouldNotContain("\"pattern\"");
        compact.GetRawText().ShouldNotContain("$schema");
        Flatten(slots).Where(s => s.GetProperty("kind").GetString() == "block").ShouldAllBe(s => blockNames.Contains(s.GetProperty("block").GetString()!));
        var emphasis = compact.GetProperty("blocks").GetProperty("emphasis").GetProperty("slots").EnumerateArray().Single();
        emphasis.GetProperty("kind").GetString().ShouldBe("list");
        emphasis.GetProperty("max").GetInt32().ShouldBe(3);
        emphasis.GetProperty("item").GetProperty("slots").EnumerateArray().Select(f => f.GetProperty("name").GetString()).ShouldBe(["text", "tone", "in"]);
        var icon = Flatten(slots).First(s => s.GetProperty("kind").GetString() == "icon");
        icon.TryGetProperty("values", out _).ShouldBeFalse();
        var headline = layouts.First(l => l.GetProperty("name").GetString() == "quote").GetProperty("slots").EnumerateArray()
            .First(s => s.GetProperty("name").GetString() == "quote");
        headline.GetProperty("kind").GetString().ShouldBe("text");
        headline.GetProperty("max").GetInt32().ShouldBe(280);
    }

    [Test]
    public async Task ListLayouts_WhenCompactIsOmitted_ShouldReturnTheFullSchema()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var omitted = await SlideCalls.OkAsync(agent, "list_layouts", new { });
        var off = await SlideCalls.OkAsync(agent, "list_layouts", new { compact = false });

        off.GetRawText().ShouldBe(omitted.GetRawText());
        var first = omitted.GetProperty("layouts").EnumerateArray().First();
        first.GetProperty("schema").GetProperty("properties").ValueKind.ShouldBe(JsonValueKind.Object);
        omitted.TryGetProperty("icons", out _).ShouldBeFalse();
    }

    [Test]
    public async Task ListLayouts_WhenCompactNamesAnUnknownTemplate_ShouldReturnTodaysError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        (await SlideCalls.ErrorAsync(agent, "list_layouts", new { template = "nope", compact = true })).ShouldBe("Unknown template 'nope'");
    }

    [Test]
    public async Task GetDeck_WhenSummary_ShouldOmitSlotsAndNotes()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await SummaryDeckAsync(agent);

        var full = await SlideCalls.OkAsync(agent, "get_deck", new { deck });
        var summary = await SlideCalls.OkAsync(agent, "get_deck", new { deck, summary = true });

        summary.EnumerateObject().Select(p => p.Name).ShouldBe(full.EnumerateObject().Select(p => p.Name));
        foreach (var name in new[] { "id", "slug", "title", "subtitle", "author", "parts", "sharing", "url", "look", "talk_minutes" })
        {
            summary.GetProperty(name).GetRawText().ShouldBe(full.GetProperty(name).GetRawText());
        }

        var slides = summary.GetProperty("slides").EnumerateArray().ToList();
        slides.Select(s => (s.GetProperty("number").GetInt32(), s.GetProperty("layout").GetString(), s.GetProperty("headline").GetString())).ShouldBe(
        [
            (1, "divider", "The cost"),
            (2, "quote", "Small changes ship."),
            (3, "quote", LongQuote[..120]),
        ]);
        slides.ShouldAllBe(s => s.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] { "number", "layout", "part", "headline" }));
        slides[0].GetProperty("part").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task GetDeck_WhenSummaryIsOmitted_ShouldReturnSlots()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await SummaryDeckAsync(agent);

        var omitted = await SlideCalls.OkAsync(agent, "get_deck", new { deck });
        var off = await SlideCalls.OkAsync(agent, "get_deck", new { deck, summary = false });

        off.GetRawText().ShouldBe(omitted.GetRawText());
        var slide = omitted.GetProperty("slides").EnumerateArray().ElementAt(1);
        slide.GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Small changes ship.");
        slide.GetProperty("notes").GetString().ShouldBe("Say it slowly");
        slide.TryGetProperty("revision", out _).ShouldBeTrue();
    }

    [Test]
    public async Task GetDeck_WhenSummaryNamesAnUnknownDeck_ShouldReturnTodaysError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var today = await SlideCalls.ErrorAsync(agent, "get_deck", new { deck = "dck_missing" });

        (await SlideCalls.ErrorAsync(agent, "get_deck", new { deck = "dck_missing", summary = true })).ShouldBe(today);
    }

    private static readonly string LongQuote = string.Join(' ', Enumerable.Repeat("small", 22));

    // Every slot with the slots inside it: a list's item, an object's fields.
    private static IEnumerable<JsonElement> Flatten(IEnumerable<JsonElement> slots)
        => slots.SelectMany(slot => new[] { slot }
            .Concat(slot.TryGetProperty("item", out var item) ? Flatten([item]) : [])
            .Concat(slot.TryGetProperty("slots", out var inner) ? Flatten(inner.EnumerateArray()) : []));

    // A divider (title), a quote, and a quote whose words run past the headline's 120 characters.
    private static async Task<string> SummaryDeckAsync(McpTestClient agent)
    {
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: DeckSeed.SAMPLE_PARTS, author: "Sam Rivera");
        await DeckSeed.AddSlideAsync(agent, deck, "divider", """{"title":"The cost"}""", part: 1);
        await DeckSeed.AddSlideAsync(agent, deck, "quote", """{"quote":"Small changes ship.","attribution":"Sam Rivera"}""", notes: "Say it slowly");
        await DeckSeed.AddQuoteAsync(agent, deck, LongQuote);
        return deck;
    }
}
