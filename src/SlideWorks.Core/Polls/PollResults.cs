using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// When the room sees the counts (V1, D4): live (the default), after a guest has answered (that guest only), or after the
// poll closes. PollVisibility applies it. On the wire it is "live", "afterAnswer" or "afterClose".
[JsonConverter(typeof(JsonStringEnumConverter<PollResults>))]
public enum PollResults
{
    [JsonStringEnumMemberName("live")]
    Live,

    [JsonStringEnumMemberName("afterAnswer")]
    AfterAnswer,

    [JsonStringEnumMemberName("afterClose")]
    AfterClose,
}
