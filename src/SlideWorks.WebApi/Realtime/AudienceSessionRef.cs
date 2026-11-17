namespace SlideWorks.WebApi.Realtime;

// What IAudienceSessions finds for a code or a deck (J3): the deck, the open session's id (null unless the phase is live)
// and the phase.
public sealed record AudienceSessionRef(string DeckId, string? SessionId, AudiencePhase Phase);
