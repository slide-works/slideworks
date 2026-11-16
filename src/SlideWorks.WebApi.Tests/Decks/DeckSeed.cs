using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Activity;
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

    // A deck's change log, newest first, read through its own interface (K7) until the deck page shows it.
    public static async Task<IReadOnlyList<ActivityEntryRow>> ActivityAsync(WebApplicationFactory<ApiMarker> factory, string deckId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IActivityLog>().ListAsync(deckId, 50);
    }
}
