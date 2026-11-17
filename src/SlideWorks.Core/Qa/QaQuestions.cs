using System.Text.Json.Serialization;

namespace SlideWorks.Core.Qa;

// Whether the room can ask (Q1, D2): open (the default), closed (the list and upvotes stay, no new questions) or off (no
// Questions tab and no round). On the wire it is "open", "closed" or "off".
[JsonConverter(typeof(JsonStringEnumConverter<QaQuestions>))]
public enum QaQuestions
{
    [JsonStringEnumMemberName("open")]
    Open,

    [JsonStringEnumMemberName("closed")]
    Closed,

    [JsonStringEnumMemberName("off")]
    Off,
}
