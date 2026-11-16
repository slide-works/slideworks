using System.Text.Json;
using SlideWorks.WebApi.Tests.Mcp;

namespace SlideWorks.WebApi.Tests.Slides;

// The slide edit tools (update_slide, move_slide, delete_slide, set_notes) and get_deck, called the way an agent would.
public static class SlideCalls
{
    // The tool's result object; fails the test on a tool error.
    public static async Task<JsonElement> OkAsync(McpTestClient agent, string tool, object args)
    {
        var (isError, json) = await agent.CallToolAsync(tool, args);
        isError.ShouldBeFalse(json.ToString());
        return json;
    }

    // The tool error's text; fails the test on a success.
    public static async Task<string> ErrorAsync(McpTestClient agent, string tool, object args)
    {
        var (isError, json) = await agent.CallToolAsync(tool, args);
        isError.ShouldBeTrue(json.ToString());
        return json.GetString()!;
    }

    public static async Task<JsonElement> UpdateAsync(McpTestClient agent, string deck, int slide, long revision, string slotsJson, string? layout = null, int? part = null)
    {
        using var slots = JsonDocument.Parse(slotsJson);
        return await OkAsync(agent, "update_slide", new { deck, slide, revision, slots = slots.RootElement, layout, part });
    }

    public static async Task<string> UpdateErrorAsync(McpTestClient agent, string deck, int slide, long revision, string slotsJson, string? layout = null, int? part = null)
    {
        using var slots = JsonDocument.Parse(slotsJson);
        return await ErrorAsync(agent, "update_slide", new { deck, slide, revision, slots = slots.RootElement, layout, part });
    }

    // get_deck's live slides, in order.
    public static async Task<IReadOnlyList<JsonElement>> SlidesAsync(McpTestClient agent, string deck)
        => (await OkAsync(agent, "get_deck", new { deck })).GetProperty("slides").EnumerateArray().ToList();

    public static IEnumerable<string?> Quotes(IEnumerable<JsonElement> slides)
        => slides.Select(s => s.GetProperty("slots").GetProperty("quote").GetString());

    public static IEnumerable<long> Revisions(IEnumerable<JsonElement> slides)
        => slides.Select(s => s.GetProperty("revision").GetInt64());

    public static IEnumerable<int> Numbers(IEnumerable<JsonElement> slides)
        => slides.Select(s => s.GetProperty("number").GetInt32());

    // Seeds a deck with one quote slide per text, in order; slide n gets revision n.
    public static async Task<string> DeckOfQuotesAsync(McpTestClient agent, params string[] quotes)
    {
        var deck = await Decks.DeckSeed.CreateDeckAsync(agent);
        foreach (var quote in quotes)
        {
            await Decks.DeckSeed.AddQuoteAsync(agent, deck, quote);
        }
        return deck;
    }
}
