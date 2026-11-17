using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// Where a poll's current round is in this audience session (V4): draft (no round yet, or a new round not yet opened),
// open (answers come in) or closed (the final result is kept). On the wire it is "draft", "open" or "closed".
[JsonConverter(typeof(JsonStringEnumConverter<PollPhase>))]
public enum PollPhase
{
    [JsonStringEnumMemberName("draft")]
    Draft,

    [JsonStringEnumMemberName("open")]
    Open,

    [JsonStringEnumMemberName("closed")]
    Closed,
}
