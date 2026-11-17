using System.Text.Json.Serialization;

namespace SlideWorks.Core.Qa;

// A question's state as one value where hidden must show (Q4's GuestQuestion.state for the guest's own question, Q10's
// QuestionsReport): waiting, open, answered, or hidden, which wins over the state under it. On the wire it is "waiting",
// "open", "answered" or "hidden".
[JsonConverter(typeof(JsonStringEnumConverter<QuestionStatus>))]
public enum QuestionStatus
{
    [JsonStringEnumMemberName("waiting")]
    Waiting,

    [JsonStringEnumMemberName("open")]
    Open,

    [JsonStringEnumMemberName("answered")]
    Answered,

    [JsonStringEnumMemberName("hidden")]
    Hidden,
}
