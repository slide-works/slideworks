namespace SlideWorks.WebApi.Features.Decks.Views;

// GET /api/decks: the decks library screen (src/contracts/screens/decks-library.json). The caller's own decks, the
// decks shared with them by name, and whether to show the "connect an agent" card.
public sealed record DecksLibraryView(IReadOnlyList<DeckRow> Decks, IReadOnlyList<SharedDeckRow> SharedWithMe, ConnectAgent Connect);
