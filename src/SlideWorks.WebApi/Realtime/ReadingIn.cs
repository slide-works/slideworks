using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Realtime;

// Guests connected now, by the language they read the deck in (SW-886 K11): sent to audience-watch:{deckId} only, never to
// a guest. Counts is null when translations were turned off, which hides the presenter's counter. Only IReadingIn's pump
// publishes it.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ReadingIn), "readingIn")]
public record ReadingIn(string DeckId, IReadOnlyList<ReadingInCount>? Counts) : AudienceEvent(DeckId);
