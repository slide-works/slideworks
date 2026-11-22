using System.Globalization;
using System.Text;
using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Look.CustomLayouts.Schema;

// Reads a slots.json into a checked schema and the slot types slides are validated with. It never throws
// for what an author wrote: every problem is an error with a path, up to MAX_ERRORS of them. A path is
// slots.<slot>[.of.<field>...]; a name that did not pass its own pattern never reaches a path or a message,
// the error is put on the slot's parent instead.
internal sealed class SlotSchemaReader
{
    private const string DOCUMENT = "slots";

    private static readonly string[] DocumentKeys = ["name", "description", "surface", "steps", "slots"];
    private static readonly string[] Surfaces = ["light", "dark", "either"];
    private static readonly string[] BlockTypes = ["callout", "chip", "badge", "levels", "facts"];

    private readonly List<CustomLayoutError> _errors = [];
    private int _slotCount;
    private bool _tooMany;

    public IReadOnlyList<CustomLayoutError> Errors => _errors;

    public CustomLayoutSchema? Schema { get; private set; }

    public IReadOnlyList<SlotField> Fields { get; private set; } = [];

    public LayoutSurface Surface { get; private set; }

    // True when the schema was read with no error.
    public bool Read(string slotsJson)
    {
        if (slotsJson is null || Encoding.UTF8.GetByteCount(slotsJson) > CustomLayoutLimits.MAX_PART_BYTES)
        {
            Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_TOO_LARGE);
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(slotsJson, new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_MALFORMED);
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_MALFORMED);
                return false;
            }

            ReadDocument(document.RootElement);
        }

        return _errors.Count == 0;
    }

    private void ReadDocument(JsonElement root)
    {
        foreach (var property in root.EnumerateObject().Where(p => !DocumentKeys.Contains(p.Name, StringComparer.Ordinal)))
        {
            Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_KEY);
        }

        var name = Text(root, "name");
        if (name is null || !LayoutClassPrefix.IsLayoutName(name)
            || name.Length is < SlotSchemaLimits.MIN_LAYOUT_NAME or > SlotSchemaLimits.MAX_LAYOUT_NAME)
        {
            Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_NAME);
        }
        else if (LayoutCatalogue.LayoutNames.Contains(name, StringComparer.Ordinal))
        {
            Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_NAME, name);
        }

        var description = Text(root, "description");
        if (description is null || description.Trim().Length == 0 || description.Any(char.IsControl)
            || StringInfoLength(description) > SlotSchemaLimits.MAX_DESCRIPTION)
        {
            Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_DESCRIPTION);
        }

        var surface = Text(root, "surface");
        if (surface is null || !Surfaces.Contains(surface, StringComparer.Ordinal))
        {
            Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_SURFACE);
        }

        var slots = new List<ParsedSlot>();
        if (!root.TryGetProperty("slots", out var map) || map.ValueKind != JsonValueKind.Object)
        {
            Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_MALFORMED);
        }
        else
        {
            slots = ReadFields(map, DOCUMENT, 0);
        }

        string? steps = null;
        if (root.TryGetProperty("steps", out var stepsValue))
        {
            var target = stepsValue.ValueKind == JsonValueKind.String ? stepsValue.GetString() : null;
            if (target is not null && NamesAList(root, target))
            {
                steps = target;
            }
            else
            {
                Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_STEPS);
            }
        }

        if (_errors.Count > 0 || name is null || description is null || surface is null)
        {
            return;
        }

        Surface = surface switch
        {
            "light" => LayoutSurface.Light,
            "dark" => LayoutSurface.Dark,
            _ => LayoutSurface.Either,
        };
        Fields = [.. slots.Select(s => new SlotField(s.Slot.Name, s.Type, !s.Slot.Optional))];
        Schema = new CustomLayoutSchema(name, description, surface, steps, [.. slots.Select(s => s.Slot)]);
    }

    // The slots of one map: the top-level slots (level 0), or the fields of a list's items (level 1 or 2).
    private List<ParsedSlot> ReadFields(JsonElement map, string path, int level)
    {
        var result = new List<ParsedSlot>();
        foreach (var property in map.EnumerateObject())
        {
            if (_tooMany)
            {
                break;
            }

            if (!SlotNames.IsSlotName(property.Name))
            {
                if (SlotNames.IsReserved(property.Name))
                {
                    Fail(path, CustomLayoutErrorCodes.SLOTS_NAME, property.Name);
                }
                else
                {
                    Fail(path, CustomLayoutErrorCodes.SLOTS_NAME);
                }

                continue;
            }

            var parsed = ReadSlot(property.Name, property.Value, $"{path}.{property.Name}", level, scalarOnly: false);
            if (parsed is not null)
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    // level: 0 top-level, 1 a field of a top-level list's items, 2 a field of an inner list's items.
    private ParsedSlot? ReadSlot(string name, JsonElement definition, string path, int level, bool scalarOnly)
    {
        if (++_slotCount > CustomLayoutLimits.MAX_SLOTS)
        {
            // Stop at the first slot over the limit: the rest of the schema is not read.
            if (!_tooMany)
            {
                _tooMany = true;
                Fail(DOCUMENT, CustomLayoutErrorCodes.SLOTS_TOO_MANY);
            }

            return null;
        }

        if (definition.ValueKind != JsonValueKind.Object
            || !definition.TryGetProperty("type", out var typeValue) || typeValue.ValueKind != JsonValueKind.String)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_TYPE);
            return null;
        }

        var type = typeValue.GetString()!;
        var scalar = type is "text" or "number" or "enum";
        if (scalarOnly && !scalar)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_TYPE);
            return null;
        }

        if (type == CustomSlot.LIST && level >= SlotSchemaLimits.MAX_LIST_LEVELS)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_DEPTH);
            return null;
        }

        if ((type == "image" || BlockTypes.Contains(type, StringComparer.Ordinal)) && level > 0)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_TYPE);
            return null;
        }

        if (type == "callout" && name != "callout")
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_TYPE);
            return null;
        }

        return type switch
        {
            "text" => ReadText(name, definition, path, scalarOnly),
            "number" => ReadNumber(name, definition, path, scalarOnly),
            "enum" => ReadEnum(name, definition, path, scalarOnly),
            CustomSlot.LIST => ReadList(name, definition, path, level),
            "image" => ReadImage(name, definition, path),
            _ when BlockTypes.Contains(type, StringComparer.Ordinal) => ReadBlock(name, type, definition, path),
            _ => Unknown(path),
        };
    }

    private ParsedSlot? Unknown(string path)
    {
        Fail(path, CustomLayoutErrorCodes.SLOTS_TYPE);
        return null;
    }

    private ParsedSlot? ReadText(string name, JsonElement definition, string path, bool scalarOnly)
    {
        var ok = Keys(definition, path, scalarOnly ? ["type", "max"] : ["type", "max", "optional"]);
        ok &= Whole(definition, "max", SlotSchemaLimits.MAX_TEXT, 1, path, out var max);
        ok &= Optional(definition, path, scalarOnly, out var optional);
        return ok ? new ParsedSlot(new CustomSlot(name, "text", optional, null, max, [], [], null), new TextSlot(max)) : null;
    }

    private ParsedSlot? ReadNumber(string name, JsonElement definition, string path, bool scalarOnly)
    {
        var ok = Keys(definition, path, scalarOnly ? ["type", "min", "max"] : ["type", "min", "max", "optional"]);
        ok &= Optional(definition, path, scalarOnly, out var optional);
        var min = 0.0;
        var max = 0.0;
        var bounded = definition.TryGetProperty("min", out var minValue) && definition.TryGetProperty("max", out var maxValue)
            && minValue.ValueKind == JsonValueKind.Number && maxValue.ValueKind == JsonValueKind.Number
            && minValue.TryGetDouble(out min) && maxValue.TryGetDouble(out max)
            && double.IsFinite(min) && double.IsFinite(max) && min < max
            && Math.Abs(min) <= SlotSchemaLimits.MAX_NUMBER_BOUND && Math.Abs(max) <= SlotSchemaLimits.MAX_NUMBER_BOUND;
        if (!bounded)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_LIMIT);
        }

        return ok && bounded
            ? new ParsedSlot(new CustomSlot(name, "number", optional, min, max, [], [], null), new NumberSlot(min, max, false))
            : null;
    }

    private ParsedSlot? ReadEnum(string name, JsonElement definition, string path, bool scalarOnly)
    {
        var ok = Keys(definition, path, scalarOnly ? ["type", "values"] : ["type", "values", "optional"]);
        ok &= Optional(definition, path, scalarOnly, out var optional);
        var values = new List<string>();
        var valid = definition.TryGetProperty("values", out var list) && list.ValueKind == JsonValueKind.Array
            && list.GetArrayLength() is >= 1 and <= SlotSchemaLimits.MAX_ENUM_VALUES;
        if (valid)
        {
            foreach (var value in list.EnumerateArray())
            {
                var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : null;
                valid &= text is not null && SlotNames.IsEnumValue(text) && !values.Contains(text, StringComparer.Ordinal);
                if (text is not null)
                {
                    values.Add(text);
                }
            }
        }

        if (!valid)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_LIMIT);
        }

        return ok && valid ? new ParsedSlot(new CustomSlot(name, CustomSlot.ENUM, optional, null, null, values, [], null), new EnumSlot([.. values])) : null;
    }

    private ParsedSlot? ReadList(string name, JsonElement definition, string path, int level)
    {
        var ok = Keys(definition, path, ["type", "min", "max", "of"]);
        var limits = Whole(definition, "max", SlotSchemaLimits.MAX_LIST_ITEMS, SlotSchemaLimits.MIN_LIST_ITEMS, path, out var max);
        var hasMin = Whole(definition, "min", max, 0, path, out var min);
        ok &= limits && hasMin;

        if (!definition.TryGetProperty("of", out var of) || of.ValueKind != JsonValueKind.Object)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_TYPE);
            return null;
        }

        var ofPath = $"{path}.of";
        ParsedSlot? item = null;
        List<ParsedSlot> fields = [];
        var scalar = of.TryGetProperty("type", out var ofType) && ofType.ValueKind == JsonValueKind.String;
        if (scalar)
        {
            item = ReadSlot(CustomSlot.ITEM, of, ofPath, level + 1, scalarOnly: true);
            ok &= item is not null;
        }
        else
        {
            var before = _errors.Count;
            fields = ReadFields(of, ofPath, level + 1);
            ok &= _errors.Count == before;
        }

        if (!ok)
        {
            return null;
        }

        ISlotType itemType = scalar ? item!.Type : new ObjectSlot([.. fields.Select(f => new SlotField(f.Slot.Name, f.Type, !f.Slot.Optional))]);
        return new ParsedSlot(
            new CustomSlot(name, CustomSlot.LIST, false, min, max, [], [.. fields.Select(f => f.Slot)], scalar ? item!.Slot : null),
            new ListSlot(itemType, min, max));
    }

    private ParsedSlot? ReadImage(string name, JsonElement definition, string path)
    {
        var ok = Keys(definition, path, ["type", "optional"]);
        if (!definition.TryGetProperty("optional", out var optional) || optional.ValueKind != JsonValueKind.True)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_LIMIT);
            ok = false;
        }

        return ok ? new ParsedSlot(new CustomSlot(name, "image", true, null, null, [], [], null), new ImageSlot()) : null;
    }

    private ParsedSlot? ReadBlock(string name, string type, JsonElement definition, string path)
    {
        var ok = Keys(definition, path, ["type", "optional"]);
        ok &= Optional(definition, path, false, out var optional);
        return ok
            ? new ParsedSlot(new CustomSlot(name, type, optional, null, null, [], [], null), new BlockSlot(Block(type)))
            : null;
    }

    private static InlineBlock Block(string type) => type switch
    {
        "callout" => InlineBlocks.Callout,
        "chip" => InlineBlocks.Chip,
        "badge" => InlineBlocks.Badge,
        "levels" => InlineBlocks.Levels,
        _ => InlineBlocks.Facts,
    };

    // Every key of the slot must be one of the allowed.
    private bool Keys(JsonElement definition, string path, string[] allowed)
    {
        var unknown = definition.EnumerateObject().Count(p => !allowed.Contains(p.Name, StringComparer.Ordinal));
        for (var i = 0; i < unknown; i++)
        {
            Fail(path, CustomLayoutErrorCodes.SLOTS_KEY);
        }

        return unknown == 0;
    }

    // A whole number between low and high (inclusive) at the key.
    private bool Whole(JsonElement definition, string key, int high, int low, string path, out int value)
    {
        value = 0;
        if (definition.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value) && value >= low && value <= high)
        {
            return true;
        }

        Fail(path, CustomLayoutErrorCodes.SLOTS_LIMIT);
        return false;
    }

    private bool Optional(JsonElement definition, string path, bool scalarOnly, out bool optional)
    {
        optional = false;
        if (scalarOnly || !definition.TryGetProperty("optional", out var element))
        {
            return true;
        }

        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            optional = element.ValueKind == JsonValueKind.True;
            return true;
        }

        Fail(path, CustomLayoutErrorCodes.SLOTS_LIMIT);
        return false;
    }

    // Whether the document's slots hold a top-level slot of this name whose type is list.
    private static bool NamesAList(JsonElement root, string name) =>
        root.TryGetProperty("slots", out var map) && map.ValueKind == JsonValueKind.Object
        && map.TryGetProperty(name, out var slot) && slot.ValueKind == JsonValueKind.Object
        && slot.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
        && type.GetString() == CustomSlot.LIST;

    private static string? Text(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int StringInfoLength(string text) => new StringInfo(text).LengthInTextElements;

    private void Fail(string path, string code, string? identifier = null)
    {
        if (_errors.Count >= SlotSchemaLimits.MAX_ERRORS)
        {
            return;
        }

        _errors.Add(identifier is not null && SlotNames.IsSafeToQuote(identifier)
            ? CustomLayoutError.Of(path, code, identifier)
            : CustomLayoutError.Of(path, code));
    }
}
