using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_layouts with detail "compact": { layouts, icons, blocks } (SW-568). No JSON Schema and no $defs. The icon names and every block a layout names are stated once
// here, and no layout repeats them: the catalogue's inline blocks, emphasis, and any block a layout carries of its own.
public sealed record CompactListLayoutsResult(IReadOnlyList<CompactLayoutInfo> Layouts, IReadOnlyList<string> Icons, IReadOnlyDictionary<string, CompactBlockInfo> Blocks)
{
    public static CompactListLayoutsResult From(IEnumerable<LayoutDefinition> layouts)
    {
        var all = layouts.ToList();
        var blocks = InlineBlocks.All.ToDictionary(block => block.Name, CompactBlockInfo.From, StringComparer.Ordinal);
        blocks[CompactBlockInfo.EMPHASIS] = CompactBlockInfo.Emphasis();
        foreach (var own in all.SelectMany(layout => layout.Slots.Fields).SelectMany(field => OwnBlocks(field.Type)))
        {
            blocks.TryAdd(own.Name, CompactBlockInfo.From(own));
        }

        return new([.. all.Select(CompactLayoutInfo.From)], IconCatalogue.Names, blocks);
    }

    // The blocks a slot holds, however deep in lists and objects.
    private static IEnumerable<InlineBlock> OwnBlocks(ISlotType type) => type switch
    {
        BlockSlot block => [block.Block],
        ListSlot list => OwnBlocks(list.Item),
        ObjectSlot obj => obj.Fields.SelectMany(field => OwnBlocks(field.Type)),
        TextOrObjectSlot either => either.Richer.Fields.SelectMany(field => OwnBlocks(field.Type)),
        _ => [],
    };
}
