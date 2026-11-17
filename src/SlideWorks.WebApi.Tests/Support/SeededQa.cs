namespace SlideWorks.WebApi.Tests.Support;

// What QaSeed made: the deck, the open audience session, its join code and its open Q&A round, and the deck's two slides by
// 1-based number and id: QuoteSlide (where the presenter is) and QaSlide (the qa layout).
public sealed record SeededQa(string DeckId, Guid SessionId, string Code, Guid RoundId, int QuoteSlide, Guid QuoteSlideId, int QaSlide, Guid QaSlideId);
