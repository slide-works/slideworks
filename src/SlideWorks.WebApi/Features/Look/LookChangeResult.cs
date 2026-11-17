using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Look;

// PUT /api/decks/{id}/look's 200 (L6 look-change.json): the deck page's look after the change, and a warning for each
// slide whose layout is outside the deck's template (L7, D9).
public sealed record LookChangeResult(DeckLookView Look, IReadOnlyList<string> Warnings);
