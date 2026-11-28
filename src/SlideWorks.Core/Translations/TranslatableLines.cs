using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Translations;

// The lines of a slide that a language can translate (K3): every text-slot string in the stored slots,
// in document order, found by walking the layout's slot types rather than the raw JSON, so enum,
// number, image, icon, link and YouTube values are never lines. A string item is "/items/0"; the text
// of an object item is "/items/0/text"; an emphasis phrase is "/emphasis/0/text". An empty string (a
// table's row-label header) has nothing to translate and is not a line.
public static class TranslatableLines
{
    public static IReadOnlyList<SlotLine> Of(LayoutDefinition layout, JsonElement slots)
    {
        var lines = new List<SlotLine>();
        Walk(layout.Slots, slots, "", lines);
        return lines;
    }

    private static void Walk(ISlotType type, JsonElement value, string path, List<SlotLine> lines)
    {
        switch (type)
        {
            case TextSlot when value.ValueKind == JsonValueKind.String:
                AddText(value, path, lines);
                break;
            case TextOrObjectSlot either:
                Walk(value.ValueKind == JsonValueKind.Object ? either.Richer : either.Text, value, path, lines);
                break;
            case ObjectSlot shape when value.ValueKind == JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    var field = shape.Fields.FirstOrDefault(f => string.Equals(f.Name, property.Name, StringComparison.Ordinal));
                    if (field is not null)
                    {
                        Walk(field.Type, property.Value, SlotPointer.Append(path, property.Name), lines);
                    }
                }

                break;
            case ListSlot list when value.ValueKind == JsonValueKind.Array:
                WalkItems(list.Item, value, path, lines);
                break;
            case EmphasisSlot when value.ValueKind == JsonValueKind.Array:
                AddPhrases(value, path, lines);
                break;
            case BlockSlot { Block.Type: { } blockType }:
                Walk(blockType, value, path, lines);
                break;
        }
    }

    private static void WalkItems(ISlotType item, JsonElement value, string path, List<SlotLine> lines)
    {
        var index = 0;
        foreach (var element in value.EnumerateArray())
        {
            Walk(item, element, SlotPointer.Append(path, index), lines);
            index++;
        }
    }

    // An emphasis entry's phrase is a line; its tone and target are not.
    private static void AddPhrases(JsonElement entries, string path, List<SlotLine> lines)
    {
        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty("text", out var phrase)
                && phrase.ValueKind == JsonValueKind.String)
            {
                AddText(phrase, SlotPointer.Append(SlotPointer.Append(path, index), "text"), lines);
            }

            index++;
        }
    }

    private static void AddText(JsonElement value, string path, List<SlotLine> lines)
    {
        var text = value.GetString()!;
        if (text.Length > 0)
        {
            lines.Add(new SlotLine(path, text));
        }
    }
}
