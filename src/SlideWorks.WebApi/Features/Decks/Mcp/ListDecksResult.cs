namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_decks: { decks: [...] }. A wrapper object, because MCP clients refuse structured content that is an array.
public sealed record ListDecksResult(IReadOnlyList<ListedDeck> Decks);
