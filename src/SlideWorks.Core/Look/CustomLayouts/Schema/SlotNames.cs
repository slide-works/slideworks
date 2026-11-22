namespace SlideWorks.Core.Look.CustomLayouts.Schema;

// The grammars of names in slots.json.
internal static class SlotNames
{
    private static readonly string[] Reserved = ["reveal", "surface", "emphasis", CustomSlot.ITEM];

    // [a-z][a-z0-9_]*, at most MAX_NAME characters, and none of the names the layout system owns.
    public static bool IsSlotName(string name) =>
        IsShape(name, '_') && !Reserved.Contains(name, StringComparer.Ordinal);

    public static bool IsReserved(string name) => IsShape(name, '_') && Reserved.Contains(name, StringComparer.Ordinal);

    // [a-z][a-z0-9-]*: an enum value, which becomes a class name.
    public static bool IsEnumValue(string value) => IsShape(value, '-');

    // A name that may follow an error's text: it has passed its own pattern.
    public static bool IsSafeToQuote(string name) => name.Length is > 0 and <= CustomLayoutError.MAX_IDENTIFIER
        && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_');

    private static bool IsShape(string name, char extra) =>
        name.Length is > 0 and <= SlotSchemaLimits.MAX_NAME
        && name[0] is >= 'a' and <= 'z'
        && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') || c == extra);
}
