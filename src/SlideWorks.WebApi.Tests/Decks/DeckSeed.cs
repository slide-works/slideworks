using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Mcp;

namespace SlideWorks.WebApi.Tests.Decks;

// Test decks made through the public tools only (K5): create_deck and add_slide, as an agent.
public static class DeckSeed
{
    public static readonly string[] SAMPLE_PARTS = ["The cost", "Slice it", "Ship it", "Make it stick"];

    public static async Task<string> CreateDeckAsync(McpTestClient agent, string title = "Ship smaller", string[]? parts = null, string? author = null)
    {
        var (isError, json) = await agent.CallToolAsync("create_deck", new { title, parts, author });
        isError.ShouldBeFalse(json.ToString());
        return json.GetProperty("deck").GetString()!;
    }

    public static async Task<JsonElement> AddSlideAsync(McpTestClient agent, string deck, string layout, string slotsJson, int? part = null, int? position = null, string? notes = null)
    {
        using var slots = JsonDocument.Parse(slotsJson);
        var (isError, json) = await agent.CallToolAsync("add_slide", new { deck, layout, slots = slots.RootElement, part, position, notes });
        isError.ShouldBeFalse(json.ToString());
        return json;
    }

    public static Task<JsonElement> AddQuoteAsync(McpTestClient agent, string deck, string quote, int? position = null)
        => AddSlideAsync(agent, deck, "quote", JsonSerializer.Serialize(new { quote, attribution = "Sam Rivera" }), position: position);

    // The sample deck's slides (K3) that need no uploaded image: every one but the two-column slide with its picture.
    public static IReadOnlyList<JsonElement> SampleSlidesWithoutImages()
    {
        var path = Path.Combine(Support.JsonShape.ScreensDirectory(), "..", "sample-deck.json");
        using var sample = JsonDocument.Parse(File.ReadAllText(path));
        return sample.RootElement.GetProperty("slides").EnumerateArray()
            .Where(slide => !slide.GetProperty("slots").GetRawText().Contains("\"image\"", StringComparison.Ordinal))
            .Select(slide => slide.Clone())
            .ToList();
    }

    // A deck's change log, newest first, as its owner sees it on the deck page: the activity[] of GET /api/decks/{id}.
    public static async Task<IReadOnlyList<JsonElement>> ActivityAsync(HttpClient owner, string deckId)
    {
        var response = await owner.GetAsync($"/api/decks/{deckId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        return page.GetProperty("activity").EnumerateArray().Select(entry => entry.Clone()).ToList();
    }
}
