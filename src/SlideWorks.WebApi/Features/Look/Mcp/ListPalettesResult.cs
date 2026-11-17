namespace SlideWorks.WebApi.Features.Look.Mcp;

// list_palettes: { palettes: [...] }, the palettes that match the filters, possibly none. A wrapper object, because MCP
// clients refuse structured content that is an array.
public sealed record ListPalettesResult(IReadOnlyList<ListPalettesEntry> Palettes);
