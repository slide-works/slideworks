namespace SlideWorks.WebApi.Features.Templates.Views;

// Whether the layout stays inside the safe area at its maximum; Overflow names what does not.
public sealed record LayoutReviewSafeArea(bool Ok, IReadOnlyList<string> Overflow);
