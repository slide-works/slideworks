using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One inline block in a compact list_layouts, stated once under blocks.<name>: its slots. A block that is one value (a chip) has one slot named after it; journey, which agents never set, has none.
public sealed record CompactBlockInfo(IReadOnlyList<CompactSlot> Slots)
{
    public static CompactBlockInfo From(InlineBlock block) => new(block.Type switch
    {
        null => [],
        ObjectSlot obj => CompactSlot.FieldsOf(obj.Fields),
        var type => [CompactSlot.From(block.Name, type, true)],
    });
}
