using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One slide's taps of each emoji in one session, in get_deck.
public sealed record GetDeckReactionSlide(int Slide, ReactionCounts Counts);
