using System.Text.Json.Serialization;

namespace SlideWorks.Core.OpenResponses;

// Which open-response slide a round belongs to (SW-8 D1): a word cloud or a wall of open-text answers. On the wire it is
// "word-cloud" or "open-text", the same as the slide's layout name.
[JsonConverter(typeof(JsonStringEnumConverter<OpenResponseKind>))]
public enum OpenResponseKind
{
    [JsonStringEnumMemberName("word-cloud")]
    WordCloud,

    [JsonStringEnumMemberName("open-text")]
    OpenText,
}
