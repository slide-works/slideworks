namespace SlideWorks.Core.Look.CustomLayouts;

// A checked slots.json. Surface is light, dark or either; Steps names the top-level list that
// reveals one item per step, or is null.
public sealed record CustomLayoutSchema(
    string Name,
    string Description,
    string Surface,
    string? Steps,
    IReadOnlyList<CustomSlot> Slots);
