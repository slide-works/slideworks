using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Realtime;

// The live session ended: sent to audience:{deckId} (J4).
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AudienceEnded), "audienceEnded")]
public record AudienceEnded(string DeckId) : AudienceEvent(DeckId);
