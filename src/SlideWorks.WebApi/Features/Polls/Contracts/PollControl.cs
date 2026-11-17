using System.Text.Json.Serialization;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls.Contracts;

// The presenter's view of the poll on screen (audience-events.json "pollControl", V5), sent only to
// audience-watch:{deckId}. Only PollPublisher publishes it.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PollControl), "pollControl")]
public record PollControl(string DeckId, PollControlView Control) : AudienceEvent(DeckId);
