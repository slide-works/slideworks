using System.Text.Json.Serialization;

namespace SlideWorks.Core.Qa;

// What the room may do with questions on the slide on screen (Q1): open, closed (the deck's setting), offHere (the slide's
// switch is off while the deck is open) or off (the deck has questions off: no tab, no round). On the wire it is "open",
// "closed", "offHere" or "off".
[JsonConverter(typeof(JsonStringEnumConverter<QaRoomQuestions>))]
public enum QaRoomQuestions
{
    [JsonStringEnumMemberName("open")]
    Open,

    [JsonStringEnumMemberName("closed")]
    Closed,

    [JsonStringEnumMemberName("offHere")]
    OffHere,

    [JsonStringEnumMemberName("off")]
    Off,
}
