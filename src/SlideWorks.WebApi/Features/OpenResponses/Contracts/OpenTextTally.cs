using System.Text.Json.Serialization;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// An open-text slide's round as it stands (audience-events.json "openText"): the same fields as WordCloudTally, with the
// answers in place of the words. Target routes it and is never written.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(OpenTextTally), "openText")]
public record OpenTextTally(
    string DeckId,
    [property: JsonIgnore] OpenResponseTarget Target,
    int Slide,
    OpenResponsePhase Phase,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Round,
    int Number,
    int Revision,
    bool Revealed,
    int Guests,
    IReadOnlyList<WallAnswer> Answers)
    : AudienceEvent(DeckId);
