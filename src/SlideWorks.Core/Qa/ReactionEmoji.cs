using System.Text.Json.Serialization;

namespace SlideWorks.Core.Qa;

// The five fixed reactions (Q7, D6). On the wire it is "thumbsUp", "heart", "laugh", "wow" or "clap".
[JsonConverter(typeof(JsonStringEnumConverter<ReactionEmoji>))]
public enum ReactionEmoji
{
    [JsonStringEnumMemberName("thumbsUp")]
    ThumbsUp,

    [JsonStringEnumMemberName("heart")]
    Heart,

    [JsonStringEnumMemberName("laugh")]
    Laugh,

    [JsonStringEnumMemberName("wow")]
    Wow,

    [JsonStringEnumMemberName("clap")]
    Clap,
}
