using System.Text.Json.Serialization;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Slides;

// One slide of an add_slides batch. Slide is its number once stored; Error is why it was left out. Poll is the settings
// just stored on a poll slide, and omitted otherwise (a refusal, or a layout that is not a poll).
public sealed record AddSlidesItemResult(
    int Index,
    bool Ok,
    int? Slide,
    int? Steps,
    long? Revision,
    string? Url,
    IReadOnlyList<string> Warnings,
    string? Error,
    IReadOnlyList<FitRegion> Fit,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PollSettings? Poll = null);
