using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// What a poll belongs to (SW-9 Q2): a slide's poll, whose options are its slide's slots, or the deck's Q&A poll, whose
// options are the room's questions. On the wire it is "slide" or "qa".
[JsonConverter(typeof(JsonStringEnumConverter<PollKind>))]
public enum PollKind
{
    [JsonStringEnumMemberName("slide")]
    Slide,

    [JsonStringEnumMemberName("qa")]
    Qa,
}
