using System.Text.Json.Serialization;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The presenter's Q&A view (audience-events.json "questionControl", Q5), sent only to audience-watch:{deckId}. Only
// QuestionPublisher publishes it.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(QuestionControl), "questionControl")]
public record QuestionControl(string DeckId, QaControlView Control) : AudienceEvent(DeckId);
