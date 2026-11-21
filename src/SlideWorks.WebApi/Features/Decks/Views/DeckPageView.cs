using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Features.Join.Views;
using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Decks.Views;

// GET /api/decks/{id}: the deck page (src/contracts/screens/deck-page.json). PlayUrl is root-relative: /d/{id}/{slug}.
// Activity is newest first, at most 50 entries. Revision is the deck's change counter; Look is the deck's look with its
// chrome overrides and contrast summary (L6).
public sealed record DeckPageView(
    string Id,
    string Slug,
    string Title,
    string? Author,
    long Revision,
    bool IsOwner,
    string PlayUrl,
    JoinView Join,
    SharingSummary Sharing,
    IReadOnlyList<DeckPart> Parts,
    DeckLookView Look,
    IReadOnlyList<DeckPageSlide> Slides,
    IReadOnlyList<ActivityRow> Activity,
    IReadOnlyList<SlideFinding> Findings);
