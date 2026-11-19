namespace SlideWorks.Core.Slides;

// Errors each name their path, such as "practice.do[2].text: required" (indexes are 0-based).
// ImageIds are the images the slots point at, for the caller to check against the deck (K11).
public sealed record SlideValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> ImageIds)
{
    // The regions the fit check measured. Empty when the slide was refused before the fit rule ran.
    public IReadOnlyList<FitRegion> Regions { get; init; } = [];

    public bool IsValid => Errors.Count == 0;

    // The text the API returns as 400 { "error": … } and MCP as a tool error.
    public string Error => string.Join("; ", Errors);
}
