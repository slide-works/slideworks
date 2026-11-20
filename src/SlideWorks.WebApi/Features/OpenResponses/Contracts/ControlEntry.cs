using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// One entry on the presenter's control view (open-response-control.json, D11). A word's Number is 1-based by count
// descending then text key; an answer's Number is the guest's stable number. Count is a word's and is left out for an
// answer.
public record ControlEntry(
    int Number,
    string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Count,
    bool Hidden);
