namespace SlideWorks.Core.Qa;

// The live session's Q&A round as QaRoomRules.For takes it: the round's id, null when no Q&A round is open, and its list
// revision (Q5), which only increases. ListRevision is an integer, 0 when there is no round.
public sealed record QaRoundState(string? Round, long ListRevision);
