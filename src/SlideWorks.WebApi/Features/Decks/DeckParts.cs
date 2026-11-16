using System.Text.Json;

namespace SlideWorks.WebApi.Features.Decks;

// A deck's parts (D5), stored as a JSON array of strings in Deck.PartsJson. A slide's part is 1-based into this list.
public static class DeckParts
{
    public const int MAX_PARTS = 8;
    public const int MAX_NAME_LENGTH = 40;

    public static IReadOnlyList<string> Read(Deck deck) => JsonSerializer.Deserialize<string[]>(deck.PartsJson) ?? [];

    public static string Write(IReadOnlyList<string> parts) => JsonSerializer.Serialize(parts);
}
