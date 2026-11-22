using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Look.CustomLayouts.Schema;

// Writes slots JSON for a schema's slot types: every limit (Maximum) or ordinary content (Sample). Pure and
// deterministic. An image is left out: SW-50 images are always optional. A block is filled from its own
// type in InlineBlocks, so no block's limits are restated here.
internal static class SlotContent
{
    private const int SAMPLE_TEXT_CAP = 60;

    // The slot types of the schema's slots, as a layout's definition has them (whole-number bounds).
    public static ObjectSlot TypesOf(CustomLayoutSchema schema) =>
        new([.. schema.Slots.Select(slot => new SlotField(slot.Name, TypeOf(slot), !slot.Optional))]);

    public static string Write(ObjectSlot slots, bool maximum) =>
        Make(slots, maximum, 0, 0, 1)!.ToJsonString();

    private static ISlotType TypeOf(CustomSlot slot) => slot.Type switch
    {
        "text" => new TextSlot((int)slot.Max!.Value),
        "number" => new NumberSlot(slot.Min!.Value, slot.Max!.Value, false),
        CustomSlot.ENUM => new EnumSlot([.. slot.Values]),
        CustomSlot.LIST => new ListSlot(
            slot.Item is { } item
                ? TypeOf(item)
                : new ObjectSlot([.. slot.Fields.Select(field => new SlotField(field.Name, TypeOf(field), !field.Optional))]),
            (int)slot.Min!.Value,
            (int)slot.Max!.Value),
        "image" => new ImageSlot(),
        "callout" => new BlockSlot(InlineBlocks.Callout),
        "chip" => new BlockSlot(InlineBlocks.Chip),
        "badge" => new BlockSlot(InlineBlocks.Badge),
        "levels" => new BlockSlot(InlineBlocks.Levels),
        _ => new BlockSlot(InlineBlocks.Facts),
    };

    // index counts items across nested lists (so enums cycle through every value); position and count are
    // the item's place in its own list (so numbers spread over their range).
    private static JsonNode? Make(ISlotType type, bool maximum, int index, int position, int count) => type switch
    {
        TextSlot text => JsonValue.Create(SampleText.Of(maximum ? text.MaxLength : Math.Min(SAMPLE_TEXT_CAP, Math.Max(1, (text.MaxLength + 1) / 2)))),
        NumberSlot number => Number(number, maximum, position, count),
        EnumSlot choice => JsonValue.Create(maximum ? Longest(choice.Values) : choice.Values[index % choice.Values.Count]),
        ListSlot list => List(list, maximum, index),
        ObjectSlot fields => Fields(fields, maximum, index, position, count),
        BlockSlot block => Make(block.Block.Type!, maximum, index, position, count),
        _ => null,
    };

    private static JsonObject Fields(ObjectSlot fields, bool maximum, int index, int position, int count)
    {
        var result = new JsonObject();
        foreach (var field in fields.Fields.Where(f => f.Type is not ImageSlot))
        {
            result[field.Name] = Make(field.Type, maximum, index, position, count);
        }

        return result;
    }

    private static JsonArray List(ListSlot list, bool maximum, int index)
    {
        var items = maximum ? list.MaxItems : Math.Min(list.MaxItems, Math.Max(1, list.MinItems + 1));
        var result = new JsonArray();
        for (var i = 0; i < items; i++)
        {
            result.Add(Make(list.Item, maximum, (index * items) + i, i, items));
        }

        return result;
    }

    // Whole-number bounds give whole samples (as before); a decimal bound gives the real maximum and a value
    // inside the range.
    private static JsonNode Number(NumberSlot number, bool maximum, int position, int count)
    {
        var whole = double.IsInteger(number.Minimum) && double.IsInteger(number.Maximum);
        if (maximum)
        {
            return whole ? JsonValue.Create((long)number.Maximum) : JsonValue.Create(number.Maximum);
        }

        // The value at the item's place between the bounds: 1/(n+1), 2/(n+1) ... n/(n+1) of the way.
        var value = number.Minimum + ((number.Maximum - number.Minimum) * (position + 1) / (count + 1));
        value = whole ? Math.Round(value, MidpointRounding.AwayFromZero) : Math.Round(value, 6, MidpointRounding.AwayFromZero);
        value = Math.Clamp(value, number.Minimum, number.Maximum);
        return whole ? JsonValue.Create((long)value) : JsonValue.Create(value);
    }

    private static string Longest(IReadOnlyList<string> values) => values.MaxBy(v => v.Length)!;
}
