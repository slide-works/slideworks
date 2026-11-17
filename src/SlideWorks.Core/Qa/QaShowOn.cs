using System.Text.Json.Serialization;

namespace SlideWorks.Core.Qa;

// Where a question the presenter shows appears (Q1, D5, D10): over whatever slide is up (the default; a Q&A slide swaps its
// list for it), or only while a Q&A slide is up. On the wire it is "anySlide" or "qaSlide".
[JsonConverter(typeof(JsonStringEnumConverter<QaShowOn>))]
public enum QaShowOn
{
    [JsonStringEnumMemberName("anySlide")]
    AnySlide,

    [JsonStringEnumMemberName("qaSlide")]
    QaSlide,
}
