using System.Text.Json.Serialization;
using SlideWorks.Core.OpenResponses;

namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// The presenter's view of a word-cloud or open-text slide (open-response-control.json): every entry, hidden ones
// included. Round is left out while Phase is draft. Results stays live and Opens stays arrival in this feature (D10).
public record OpenResponseControlView(
    OpenResponseKind Kind,
    OpenResponsePhase Phase,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Round,
    int Number,
    int Revision,
    OpenResponseResults Results,
    OpenResponseOpens Opens,
    bool Revealed,
    int Guests,
    IReadOnlyList<ControlEntry> Entries);
