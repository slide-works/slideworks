using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Slides;

// One slide of an add_slides batch. Slide is its number once stored; Error is why it was left out.
public sealed record AddSlidesItemResult(
    int Index,
    bool Ok,
    int? Slide,
    int? Steps,
    long? Revision,
    string? Url,
    IReadOnlyList<string> Warnings,
    string? Error,
    IReadOnlyList<FitRegion> Fit);
