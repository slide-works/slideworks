using System.Text.Json;

namespace SlideWorks.Core.Slides;

// Whether a cards slide draws at the large scale (K14): every card's title and text are short and no
// card has chips. The client decides the same way; src/contracts/card-scale-fixtures.json holds the cases.
public static class CardScale
{
    public const int LARGE_TITLE = 32;
    public const int LARGE_TEXT = 70;

    public static bool IsLarge(JsonElement cards) =>
        cards.ValueKind == JsonValueKind.Array && cards.EnumerateArray().All(IsLargeCard);

    private static bool IsLargeCard(JsonElement card) =>
        card.ValueKind == JsonValueKind.Object
        && Length(card, "title") <= LARGE_TITLE
        && Length(card, "text") <= LARGE_TEXT
        && !HasChips(card);

    private static int Length(JsonElement card, string name) =>
        card.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? TextSlot.Length(value.GetString()!) : 0;

    // An empty list counts as no chips.
    private static bool HasChips(JsonElement card) =>
        card.TryGetProperty("chips", out var chips) && chips.ValueKind == JsonValueKind.Array && chips.GetArrayLength() > 0;
}
