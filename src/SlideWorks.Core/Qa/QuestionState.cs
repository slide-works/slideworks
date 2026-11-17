using System.Text.Json.Serialization;

namespace SlideWorks.Core.Qa;

// A question's stored state (Q2, D13): waiting for approval, open or answered. Hidden and on screen are flags beside it, so
// Restore returns the state it had. On the wire it is "waiting", "open" or "answered".
[JsonConverter(typeof(JsonStringEnumConverter<QuestionState>))]
public enum QuestionState
{
    [JsonStringEnumMemberName("waiting")]
    Waiting,

    [JsonStringEnumMemberName("open")]
    Open,

    [JsonStringEnumMemberName("answered")]
    Answered,
}
