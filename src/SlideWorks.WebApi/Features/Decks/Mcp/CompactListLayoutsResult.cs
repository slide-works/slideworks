using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_layouts with compact: { layouts, icons, blocks }. The icon names and the inline blocks are stated once here, and no layout repeats them.
public sealed record CompactListLayoutsResult(IReadOnlyList<CompactLayoutInfo> Layouts, IReadOnlyList<string> Icons, IReadOnlyDictionary<string, CompactBlockInfo> Blocks)
{
    public static CompactListLayoutsResult From(IEnumerable<LayoutDefinition> layouts)
        => new(
            [.. layouts.Select(CompactLayoutInfo.From)],
            IconCatalogue.Names,
            InlineBlocks.All.ToDictionary(block => block.Name, CompactBlockInfo.From, StringComparer.Ordinal));
}
