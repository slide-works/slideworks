using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Realtime;

// What a guest follows (J4): a position, not content. Goes as an "audienceState" event to the slide-state group of the
// language it was composed in, audience-slide:{deckId}:{language} (SW-883 D11), and is what JoinAudience and
// GET /api/join/{code} return. Seq only increases, and every language's state for one move carries the same seq. Phase comes
// from IAudienceSessions; Current is null unless the phase is live. Revision is the deck's change counter: a guest re-reads
// GET /api/join/{code} when it moves (D10). Activity is the activity on screen (SW-6: a PollActivity), or null. Room is the
// QaRoom for the slide on screen (SW-9 Q1, Q3), null in every phase but live. Language is the language the state is composed
// in (SW-883 K5), null while translations are off. Group is the slide-state group the relay sends the state to; it never
// goes on the wire.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AudienceState), "audienceState")]
public record AudienceState(string DeckId, long Seq, AudiencePhase Phase, AudienceSlide? Current, long Revision, JsonElement? Activity, JsonElement? Room, AudienceLanguage? Language)
    : AudienceEvent(DeckId)
{
    // The slide-state group (AudienceSlideGroups.Name) the relay sends the state to. Set after any `with`, since `with` copies it.
    [JsonIgnore]
    public string? Group { get; init; }
}
