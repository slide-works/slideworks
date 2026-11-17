namespace SlideWorks.WebApi.Realtime;

// What a closed connection left behind on a roster (J4): the session it was in and how many distinct guests remain.
public sealed record RosterLeave(string DeckId, string SessionId, int Count);
