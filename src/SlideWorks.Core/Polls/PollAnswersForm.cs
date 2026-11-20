using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// How a poll slide collects answers. Choice is the option list. Text and cloud take a free answer, shown as a list or a
// word cloud. On the wire it is "choice", "text" or "cloud".
[JsonConverter(typeof(JsonStringEnumConverter<PollAnswersForm>))]
public enum PollAnswersForm
{
    [JsonStringEnumMemberName("choice")]
    Choice,

    [JsonStringEnumMemberName("text")]
    Text,

    [JsonStringEnumMemberName("cloud")]
    Cloud,
}
