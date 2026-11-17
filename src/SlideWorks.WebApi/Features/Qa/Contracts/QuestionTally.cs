using System.Text.Json.Serialization;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The Q&A round's votes for the phones (audience-events.json "questionTally", Q5), to audience:{deckId}: only questions
// another guest may see (not hidden, not waiting). A phone re-reads its list when ListRevision is higher than the one it
// holds, and otherwise applies the votes. ListRevision and every count are integers. Only QuestionPublisher publishes it.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(QuestionTally), "questionTally")]
public record QuestionTally(string DeckId, string Round, long ListRevision, IReadOnlyList<QuestionVotes> Votes) : AudienceEvent(DeckId);
