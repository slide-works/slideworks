using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Realtime;

// What a guest follows (J4): a position, not content. Goes to audience:{deckId} as an "audienceState" event, and is what
// JoinAudience and GET /api/join/{code} return. Seq only increases. Phase comes from IAudienceSessions; Current is null
// unless the phase is live. Revision is the deck's change counter: a guest re-reads GET /api/join/{code} when it moves
// (D10). Activity is the activity on screen (SW-6: a PollActivity), or null. Room is the QaRoom for the slide on screen (SW-9
// Q1, Q3), null in every phase but live.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AudienceState), "audienceState")]
public record AudienceState(string DeckId, long Seq, AudiencePhase Phase, AudienceSlide? Current, long Revision, JsonElement? Activity, JsonElement? Room)
    : AudienceEvent(DeckId);
