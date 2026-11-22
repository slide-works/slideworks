using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One layout in a compact list_layouts: what add_slide needs to fill it, without the JSON Schema.
public sealed record CompactLayoutInfo(string Name, string Description, string Surface, string? Steps, bool Tracker, IReadOnlyList<SlotCapacity> Capacity, IReadOnlyList<CompactSlot> Slots, bool Custom = false)
{
    public static CompactLayoutInfo From(ListedLayout listed)
    {
        var layout = listed.Definition;
        return new(layout.Name, layout.Description, LayoutInfo.SurfaceName(layout.Surface), layout.Steps?.Phrase, layout.Tracker, LayoutCapacity.Of(layout, listed.CustomKey), CompactSlot.FieldsOf(layout.Slots.Fields), listed.Custom);
    }
}
