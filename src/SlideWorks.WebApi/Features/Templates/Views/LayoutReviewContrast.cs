namespace SlideWorks.WebApi.Features.Templates.Views;

// Whether every colour pair of the layout passes; Failures names the pairs that do not.
public sealed record LayoutReviewContrast(bool Ok, IReadOnlyList<string> Failures);
