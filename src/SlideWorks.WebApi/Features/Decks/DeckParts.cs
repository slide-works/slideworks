using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.WebApi.Features.Decks;

// A deck's parts (D5, K19), stored in Deck.PartsJson as a JSON array whose items are strings (older decks) or objects.
// A slide's part is 1-based into this list.
public static class DeckParts
{
    public const int MAX_PARTS = 8;
    public const int MAX_NAME_LENGTH = 40;
    public const int MAX_SHORT_LENGTH = 16;
    public const int MAX_CAPTION_LENGTH = 24;
    public const int MAX_MARK_LENGTH = 2;

    public static IReadOnlyList<DeckPart> Read(Deck deck)
    {
        var items = JsonSerializer.Deserialize<JsonElement[]>(deck.PartsJson) ?? [];
        return items.Select(ReadOne).ToList();
    }

    public static string Write(IReadOnlyList<DeckPart> parts)
        => new JsonArray(parts.Select(WriteOne).ToArray()).ToJsonString();

    private static DeckPart ReadOne(JsonElement item)
        => item.ValueKind == JsonValueKind.String
            ? new DeckPart(item.GetString() ?? "")
            : new DeckPart(Text(item, "name") ?? "", Text(item, "short"), Text(item, "caption"), Text(item, "mark"));

    private static string? Text(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonNode WriteOne(DeckPart part)
    {
        var node = new JsonObject { ["name"] = part.Name };
        if (part.Short is not null && part.Short != part.Name)
        {
            node["short"] = part.Short;
        }
        if (part.Caption is not null)
        {
            node["caption"] = part.Caption;
        }
        if (part.Mark is not null)
        {
            node["mark"] = part.Mark;
        }
        return node;
    }
}
