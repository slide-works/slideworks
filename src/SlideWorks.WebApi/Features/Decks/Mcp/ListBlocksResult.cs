namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_blocks: { blocks: [...] }, every inline block in the catalogue. A wrapper object, because MCP clients refuse
// structured content that is an array.
public sealed record ListBlocksResult(IReadOnlyList<BlockInfo> Blocks);
