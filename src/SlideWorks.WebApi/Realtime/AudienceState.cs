using System.Text.Json;

namespace SlideWorks.WebApi.Realtime;

// What a guest follows (J4): a position, not content. Goes to audience:{deckId} as an "audienceState" event, and is what
// JoinAudience and GET /api/join/{code} return. Seq only increases. Phase comes from IAudienceSessions; Current is null
// unless the phase is live. Revision is the deck's change counter: a guest re-reads GET /api/join/{code} when it moves
// (D10). Activity is always null in SW-5.
public sealed record AudienceState(string DeckId, long Seq, AudiencePhase Phase, AudienceSlide? Current, long Revision, JsonElement? Activity)
    : AudienceEvent(DeckId);
