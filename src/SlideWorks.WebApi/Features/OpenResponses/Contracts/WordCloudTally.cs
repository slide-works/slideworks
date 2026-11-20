using System.Text.Json.Serialization;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// A word-cloud slide's round as it stands (audience-events.json "wordCloud"). Slide is the slide's 1-based number. Round is
// the round's id and is left out while Phase is draft; Number is the round's number, or in draft the number the next round
// will get. Target routes it and is never written.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WordCloudTally), "wordCloud")]
public record WordCloudTally(
    string DeckId,
    [property: JsonIgnore] OpenResponseTarget Target,
    int Slide,
    OpenResponsePhase Phase,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Round,
    int Number,
    int Revision,
    bool Revealed,
    int Guests,
    IReadOnlyList<CloudWord> Words)
    : AudienceEvent(DeckId);
