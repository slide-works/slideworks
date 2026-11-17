using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;

namespace SlideWorks.WebApi.Tests.Presenting;

// A three-slide deck for the presenting tests, made through the public tools (K5): the sample deck's title slide with
// marked-up notes, its bullets slide (4 steps) with no notes, and its quote slide as a deep dive with plain notes.
public static class PresentingSeed
{
    public const string TITLE_NOTES = "**Open** with the story.\n- one batch\n- one customer";

    public const string QUOTE_NOTES = "Skip this if short of time.";

    public const int SLIDES = 3;

    public const int BULLETS_STEPS = 4;

    public static async Task<string> CreateDeckAsync(McpTestClient agent)
    {
        var sample = DeckSeed.SampleSlides();
        var deck = await DeckSeed.CreateDeckAsync(agent, "Ship smaller", DeckSeed.SAMPLE_PARTS, "Sam Rivera");
        await AddAsync(agent, deck, sample[0], TITLE_NOTES, deepDive: null);
        await AddAsync(agent, deck, sample[2], notes: null, deepDive: null);
        await AddAsync(agent, deck, sample[5], QUOTE_NOTES, deepDive: true);
        return deck;
    }

    private static async Task AddAsync(McpTestClient agent, string deck, JsonElement slide, string? notes, bool? deepDive)
    {
        var part = slide.GetProperty("part");
        var (isError, json) = await agent.CallToolAsync("add_slide", new
        {
            deck,
            layout = slide.GetProperty("layout").GetString(),
            slots = slide.GetProperty("slots"),
            part = part.ValueKind == JsonValueKind.Null ? (int?)null : part.GetInt32(),
            notes,
            deep_dive = deepDive,
        });
        isError.ShouldBeFalse(json.ToString());
    }
}
