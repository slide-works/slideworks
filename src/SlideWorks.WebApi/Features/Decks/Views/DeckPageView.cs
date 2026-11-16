namespace SlideWorks.WebApi.Features.Decks.Views;

// GET /api/decks/{id}: the deck page (src/contracts/screens/deck-page.json). PlayUrl is root-relative: /d/{id}/{slug}.
// Activity is newest first, at most 50 entries.
public sealed record DeckPageView(
    string Id,
    string Slug,
    string Title,
    string? Author,
    bool IsOwner,
    string PlayUrl,
    SharingSummary Sharing,
    IReadOnlyList<string> Parts,
    IReadOnlyList<DeckPageSlide> Slides,
    IReadOnlyList<ActivityRow> Activity);
