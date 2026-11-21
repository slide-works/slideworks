using System.Text.Json.Serialization;

namespace SlideWorks.Core.OpenResponses;

// When a slide's round opens (SW-8 D5, D10): on arrival at the slide, or only when the presenter opens it. The row stores
// arrival and nothing changes it in this feature. On the wire it is "arrival" or "manual".
[JsonConverter(typeof(JsonStringEnumConverter<OpenResponseOpens>))]
public enum OpenResponseOpens
{
    [JsonStringEnumMemberName("arrival")]
    Arrival,

    [JsonStringEnumMemberName("manual")]
    Manual,
}
