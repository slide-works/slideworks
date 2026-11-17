namespace SlideWorks.WebApi.Realtime;

// The live session ended: sent to audience:{deckId} (J4).
public sealed record AudienceEnded(string DeckId) : AudienceEvent(DeckId);
