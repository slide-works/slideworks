using System.Text.Json.Serialization;

namespace SlideWorks.Core.OpenResponses;

// When the room sees other guests' entries (SW-8 D10). The row stores live and nothing changes it in this feature; the
// control view still names the setting. On the wire it is "live", "afterAnswer" or "afterClose".
[JsonConverter(typeof(JsonStringEnumConverter<OpenResponseResults>))]
public enum OpenResponseResults
{
    [JsonStringEnumMemberName("live")]
    Live,

    [JsonStringEnumMemberName("afterAnswer")]
    AfterAnswer,

    [JsonStringEnumMemberName("afterClose")]
    AfterClose,
}
