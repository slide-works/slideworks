using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Slides;

// validate_slide: the fit report, and nothing written. Steps is present only when the slide fits.
public sealed record ValidateSlideResult(bool Ok, int? Steps, string? Error, IReadOnlyList<FitRegion> Fit);
