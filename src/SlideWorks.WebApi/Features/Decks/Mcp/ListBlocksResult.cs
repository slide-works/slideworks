namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_blocks: { blocks: [...], icons: [...] }: every inline block in the catalogue and the icon names an icon slot takes. A wrapper object, because MCP clients refuse
// structured content that is an array.
public sealed record ListBlocksResult(IReadOnlyList<BlockInfo> Blocks, IReadOnlyList<string> Icons);
