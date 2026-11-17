namespace SlideWorks.WebApi.Features.Polls.Contracts;

// Which round a tally change is about (V5), as IPollTallies.Apply takes it and IPollTallies.Changed yields it: the deck,
// the 1-based slide number when it changed, the slide's id, the audience session, the round and its option count.
public sealed record PollRoundRef(string DeckId, int Slide, Guid SlideId, string SessionId, string RoundId, int OptionCount);
