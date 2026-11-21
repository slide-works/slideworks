namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One audience session's reaction totals in get_deck (Q10): when it started, and for each slide that was reacted to, the
// taps of each emoji while that slide was on screen. Slides are the deck's live slides, by their number now; a deleted
// slide isn't listed. Written totals only, never who reacted.
public sealed record GetDeckReactionSession(DateTimeOffset StartedAt, IReadOnlyList<GetDeckReactionSlide> Slides);
