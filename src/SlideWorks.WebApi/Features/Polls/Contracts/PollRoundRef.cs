using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Polls.Contracts;

// Which round a tally change is about (V5), as IPollTallies.Apply takes it and IPollTallies.Changed yields it: the deck,
// the round's kind (SW-9 Q2), the 1-based slide number when it changed and the slide's id (both null for a Q&A round),
// the audience session, the round and its option count.
public sealed record PollRoundRef(string DeckId, PollKind Kind, int? Slide, Guid? SlideId, string SessionId, string RoundId, int OptionCount);
