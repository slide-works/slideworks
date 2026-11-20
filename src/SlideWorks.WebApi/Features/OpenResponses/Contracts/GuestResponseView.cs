using System.Text.Json.Serialization;
using SlideWorks.Core.OpenResponses;

namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// What a guest's phone gets for a word-cloud or open-text slide (word-cloud.json and open-text.json, D13). Round is left
// out while Phase is draft. A word cloud carries MineWords and an open-text slide MineText, the guest's own entry as stored
// ("" or [] before they send one); the other is left out.
public record GuestResponseView(
    OpenResponseKind Kind,
    OpenResponsePhase Phase,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Round,
    int Number,
    int Revision,
    string Question,
    bool Revealed,
    int Guests,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? MineWords,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MineText);
