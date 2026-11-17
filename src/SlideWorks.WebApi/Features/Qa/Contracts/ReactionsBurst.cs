using System.Text.Json.Serialization;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The reaction taps since the last batch (audience-events.json "reactions", Q7), sent only to audience-watch:{deckId}, never
// to phones, at most four times a second per deck. Slide is the 1-based slide on screen when they were tapped; every count
// is an integer. Only Reactions publishes it.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ReactionsBurst), "reactions")]
public record ReactionsBurst(string DeckId, int Slide, ReactionCounts Counts) : AudienceEvent(DeckId);
