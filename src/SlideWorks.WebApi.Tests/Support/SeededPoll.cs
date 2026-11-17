namespace SlideWorks.WebApi.Tests.Support;

// What PollSeed made: the deck, the poll slide's 1-based number and its id, the open audience session and the join code.
// OtherSlide is the deck's one non-poll slide, for a test that moves the presenter off the poll.
public sealed record SeededPoll(string DeckId, int Slide, Guid SlideId, Guid SessionId, string Code, int OtherSlide);
