namespace SlideWorks.WebApi.Realtime;

// What a closed connection left behind on a roster (J4): the session it was in and the session's joined count, which
// the leave didn't lower (SW-628).
public sealed record RosterLeave(string DeckId, string SessionId, int Count);
