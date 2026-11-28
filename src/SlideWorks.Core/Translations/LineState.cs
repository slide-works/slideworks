using System.Text.Json.Serialization;

namespace SlideWorks.Core.Translations;

// Where a line stands in one language (K3): no entry, an entry translated from the line's current text,
// or an entry translated from text the line no longer has. On the wire it is "missing", "current" or
// "stale".
[JsonConverter(typeof(JsonStringEnumConverter<LineState>))]
public enum LineState
{
    [JsonStringEnumMemberName("missing")]
    Missing,

    [JsonStringEnumMemberName("current")]
    Current,

    [JsonStringEnumMemberName("stale")]
    Stale,
}
