using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// When a poll's first round opens (V1, D2): when the presenter reaches the slide (the default), or only when they press
// Open. On the wire it is "onArrival" or "manual".
[JsonConverter(typeof(JsonStringEnumConverter<PollOpens>))]
public enum PollOpens
{
    [JsonStringEnumMemberName("onArrival")]
    OnArrival,

    [JsonStringEnumMemberName("manual")]
    Manual,
}
