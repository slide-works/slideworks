namespace SlideWorks.Core.Look.CustomLayouts;

// One slot of a custom layout, or one field of a list's items. Type is the slots.json type name.
// Min and Max are a number's range or a list's item counts; Max alone is a text's length. Values are
// an enum's. A list of objects has Fields; a list of one scalar type has Item, whose name is item.
public sealed record CustomSlot(
    string Name,
    string Type,
    bool Optional,
    int? Min,
    int? Max,
    IReadOnlyList<string> Values,
    IReadOnlyList<CustomSlot> Fields,
    CustomSlot? Item)
{
    // The name a list of scalars gives its value inside data-each.
    public const string ITEM = "item";
}
