using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// How many options a guest picks (V1): one (the default; a tap sends it, D3) or up to Max (tap to select, then Send).
// On the wire it is "one" or "upTo".
[JsonConverter(typeof(JsonStringEnumConverter<PollPick>))]
public enum PollPick
{
    [JsonStringEnumMemberName("one")]
    One,

    [JsonStringEnumMemberName("upTo")]
    UpTo,
}
