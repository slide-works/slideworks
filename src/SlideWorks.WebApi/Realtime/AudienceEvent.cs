using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Realtime;

// Something a guest or the owner's presenter view is sent on the hub method "audienceEvent" (J4). The "type" discriminator
// is the wire shape, mirrored in src/contracts/screens/audience-events.json. DeckId routes the event to a group and is
// never written: a guest learns nothing about the deck from a frame. Serialise through this base type (as DeckEventsRelay
// does for DeckEvent), or the discriminator is left off.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AudienceState), "audienceState")]
[JsonDerivedType(typeof(AudienceEnded), "audienceEnded")]
[JsonDerivedType(typeof(JoinedCount), "joinedCount")]
public abstract record AudienceEvent([property: JsonIgnore] string DeckId);
