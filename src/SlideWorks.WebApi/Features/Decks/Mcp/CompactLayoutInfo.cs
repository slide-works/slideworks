using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One layout in a compact list_layouts: what add_slide needs to fill it, without the JSON Schema.
public sealed record CompactLayoutInfo(string Name, string Description, string Surface, string? Steps, bool Tracker, IReadOnlyList<SlotCapacity> Capacity, IReadOnlyList<CompactSlot> Slots)
{
    public static CompactLayoutInfo From(LayoutDefinition layout)
        => new(layout.Name, layout.Description, LayoutInfo.SurfaceName(layout.Surface), layout.Steps?.Phrase, layout.Tracker, LayoutCapacity.Of(layout), CompactSlot.FieldsOf(layout.Slots.Fields));
}
