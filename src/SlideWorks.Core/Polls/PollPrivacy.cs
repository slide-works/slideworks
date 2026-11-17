using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// Who sees which option a guest picked (V1, D5): nobody, only totals. Named polls arrive with nicknames in a later
// Feature, so "private" is the only value for now.
[JsonConverter(typeof(JsonStringEnumConverter<PollPrivacy>))]
public enum PollPrivacy
{
    [JsonStringEnumMemberName("private")]
    Private,
}
