using System.Text.Json.Serialization;

namespace SlideWorks.Core.OpenResponses;

// Where a slide's open-response round is (SW-8): draft (no round yet), open (entries come in) or closed (the entries are
// kept). On the wire it is "draft", "open" or "closed".
[JsonConverter(typeof(JsonStringEnumConverter<OpenResponsePhase>))]
public enum OpenResponsePhase
{
    [JsonStringEnumMemberName("draft")]
    Draft,

    [JsonStringEnumMemberName("open")]
    Open,

    [JsonStringEnumMemberName("closed")]
    Closed,
}
