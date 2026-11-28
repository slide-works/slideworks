// The app's narrow view of GET /api/decks (src/contracts/screens/decks-library.json). Structural subsets: the SPA's
// DeckRow and DecksLibraryView stay in the SPA and are assignable to them. Dates arrive pre-formatted (updatedLabel).

export type DeckRowSummary = { id: string; title: string; updatedLabel: string };

export type DecksLibrarySummary = { decks: DeckRowSummary[]; sharedWithMe: DeckRowSummary[] };
