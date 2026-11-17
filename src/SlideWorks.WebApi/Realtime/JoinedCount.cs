namespace SlideWorks.WebApi.Realtime;

// How many distinct guests have a connection open in the current session: sent to audience-watch:{deckId}, at most once a
// second per deck (J4).
public sealed record JoinedCount(string DeckId, int Count) : AudienceEvent(DeckId);
