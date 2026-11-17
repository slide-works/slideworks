using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// How many options a guest picks (V1): one (the default; a tap sends it, D3) or up to Max (tap to select, then Send).
// Any (SW-9 Q2) has no maximum: a guest's upvotes in a Q&A round. On the wire it is "one", "upTo" or "any".
[JsonConverter(typeof(JsonStringEnumConverter<PollPick>))]
public enum PollPick
{
    [JsonStringEnumMemberName("one")]
    One,

    [JsonStringEnumMemberName("upTo")]
    UpTo,

    [JsonStringEnumMemberName("any")]
    Any,
}
