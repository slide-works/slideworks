using System.Text.Json.Serialization;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls.Contracts;

// How many have answered a round and, when the receiver may see them, the count for each option (audience-events.json
// "pollTally", V5). Counts is one int per option, or null while they are hidden from the receiver; Answered always goes
// (D21). Target routes it and is never written. Only PollPublisher publishes it.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PollTally), "pollTally")]
public record PollTally(
    string DeckId,
    [property: JsonIgnore] PollTallyTarget Target,
    string Round,
    int Answered,
    IReadOnlyList<int>? Counts)
    : AudienceEvent(DeckId);
