using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Realtime;

// Something that happened to one deck, published on IDeckEvents and sent to every browser that joined the deck on
// DeckHub. The "type" discriminator is the wire shape, mirrored in src/SlideWorks.Client/src/realtime/deckEvents.ts.
// Add an event as a record deriving from this one, with a [JsonDerivedType] line here and a case in deckEvents.ts.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DeckChanged), "deckChanged")]
public abstract record DeckEvent(Guid DeckId);
