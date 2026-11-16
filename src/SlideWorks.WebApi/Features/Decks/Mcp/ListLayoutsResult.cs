namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_layouts: { layouts: [...] }, every layout in the catalogue. A wrapper object, because MCP clients refuse
// structured content that is an array.
public sealed record ListLayoutsResult(IReadOnlyList<LayoutInfo> Layouts);
