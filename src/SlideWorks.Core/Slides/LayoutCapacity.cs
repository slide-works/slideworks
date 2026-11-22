using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// How many typical items each list or table slot of a layout fits. Measured by the layout's own fit
// rule, with every other list at its minimum, then cached: list_layouts asks on every call.
public static class LayoutCapacity
{
    // A sentence of the length workshop copy actually uses, not the character limit.
    public const string TYPICAL = "Customers see real progress when the team ships one small change each week";

    private static readonly ConcurrentDictionary<string, IReadOnlyList<SlotCapacity>> Cache = new(StringComparer.Ordinal);

    // A built-in layout is cached by its name. A custom layout passes a key of its own (template version plus asset id), so
    // two templates' same-named layouts, or a replaced upload, never share a measurement.
    public static IReadOnlyList<SlotCapacity> Of(LayoutDefinition layout, string? customKey = null) =>
        Cache.GetOrAdd(customKey is null ? layout.Name : $"custom:{customKey}:{layout.Name}", _ => Measure(layout));

    private static IReadOnlyList<SlotCapacity> Measure(LayoutDefinition layout)
    {
        var found = new List<SlotCapacity>();
        foreach (var field in layout.Slots.Fields)
        {
            if (field.Type is not ListSlot list)
            {
                continue;
            }

            try
            {
                var capacity = MeasureList(layout, field.Name, list);
                if (capacity is not null)
                {
                    found.Add(capacity);
                }
            }
            catch (JsonException)
            {
                // A list whose minimum cannot be built is left out rather than failing list_layouts.
            }
        }

        return found;
    }

    private static SlotCapacity? MeasureList(LayoutDefinition layout, string name, ListSlot list)
    {
        SlideValidationResult? fitted = null;
        var fits = -1;
        for (var count = list.MinItems; count <= list.MaxItems; count++)
        {
            var result = Validate(layout, name, count);
            if (!result.IsValid)
            {
                break;
            }

            fits = count;
            fitted = result;
        }

        if (fits < list.MinItems || fitted is null)
        {
            return null;
        }

        var region = fitted.Regions.LastOrDefault(candidate => candidate.Path.Contains(name, StringComparison.Ordinal));
        var itemPx = ItemPx(region, fits);
        var budget = region is null ? (int)Math.Round(SlideFit.BODY_HEIGHT) : (int)Math.Round(region.Budget);
        var shares = region?.Shares ?? [];
        var characters = TypicalLength(list.Item);
        var shared = shares.Count > 1
            ? $" {string.Join(" and ", shares)} share this region; this count leaves the others empty."
            : "";
        var note = $"{fits} items of about {characters} characters ({itemPx} px each, budget {budget} px).{shared}";
        return new SlotCapacity(name, fits, itemPx, budget, shares, note);
    }

    private static SlideValidationResult Validate(LayoutDefinition layout, string name, int count)
    {
        var root = (JsonObject)Value(layout.Slots, name, count)!;
        SyncRows(root);
        using var document = JsonDocument.Parse(root.ToJsonString());
        return SlideValidator.Validate(layout, document.RootElement, FitWidths.Default);
    }

    // A table row has one cell per column. Growing either list keeps the two the same length.
    private static void SyncRows(JsonObject root)
    {
        if (root["columns"] is not JsonArray columns || root["rows"] is not JsonArray rows || columns.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            if (row is not JsonArray cells)
            {
                continue;
            }

            JsonNode? sample = cells.Count > 0 ? cells[0]?.DeepClone() : JsonValue.Create(TypicalText(new TextSlot(40)));
            if (sample is null)
            {
                continue;
            }

            while (cells.Count < columns.Count)
            {
                cells.Add(sample.DeepClone());
            }

            while (cells.Count > columns.Count)
            {
                cells.RemoveAt(cells.Count - 1);
            }
        }
    }

    private static JsonNode? Value(ISlotType type, string? target, int count) => type switch
    {
        ObjectSlot obj => Object(obj, target, count),
        ListSlot list => List(list, target, count),
        TextSlot text => JsonValue.Create(TypicalText(text)),
        TextOrObjectSlot either => JsonValue.Create(TypicalText(either.Text)),
        EnumSlot choices => JsonValue.Create(choices.Values[0]),
        NumberSlot number => JsonValue.Create(number.WholeNumber ? (int)number.Minimum : number.Minimum),
        BlockSlot block => Value(block.Block.Type ?? throw new JsonException(block.Block.Name), null, 0),
        ImageSlot => new JsonObject { ["image"] = "img", ["alt"] = "A picture of the work" },
        LinkSlot => JsonValue.Create("https://example.com/page"),
        IconSlot => JsonValue.Create(IconCatalogue.Names[0]),
        _ => throw new JsonException(type.GetType().Name),
    };

    private static JsonObject Object(ObjectSlot slot, string? target, int count)
    {
        var chosen = slot.ExactlyOneOf.FirstOrDefault();
        var obj = new JsonObject();
        foreach (var field in slot.Fields)
        {
            var chosenOne = field.Name == chosen;
            if (field.Name != target && !field.Required && !chosenOne)
            {
                continue;
            }

            if (slot.ExactlyOneOf.Contains(field.Name) && !chosenOne)
            {
                continue;
            }

            obj[field.Name] = Value(field.Type, field.Name == target ? target : null, field.Name == target ? count : 0);
        }

        return obj;
    }

    private static JsonArray List(ListSlot list, string? target, int count)
    {
        var length = target is null ? list.MinItems : count;
        var items = new JsonArray();
        for (var index = 0; index < length; index++)
        {
            items.Add(Value(list.Item, null, 0));
        }

        return items;
    }

    private static int ItemPx(FitRegion? region, int fits)
    {
        if (region is { Parts.Count: > 0 })
        {
            return (int)Math.Round(region.Parts.Average(part => part.Height));
        }

        return region is not null && fits > 0 ? (int)Math.Round(region.Height / fits) : 0;
    }

    private static string TypicalText(TextSlot slot)
    {
        if (slot.AllowEmpty)
        {
            return "Label";
        }

        var text = TYPICAL.Length <= slot.MaxLength ? TYPICAL : TYPICAL[..slot.MaxLength];
        return text.Length == 0 ? "A" : text;
    }

    private static int TypicalLength(ISlotType item) => item switch
    {
        TextSlot text => Math.Min(TYPICAL.Length, text.MaxLength),
        TextOrObjectSlot either => TypicalLength(either.Text),
        ObjectSlot obj => obj.Fields.Select(field => field.Type).OfType<TextSlot>().Select(TypicalLength).DefaultIfEmpty(TYPICAL.Length).Max(),
        ListSlot list => TypicalLength(list.Item),
        _ => TYPICAL.Length,
    };
}
