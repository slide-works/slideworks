namespace SlideWorks.Core.Look.Manifest;

// A manifest's logos: the file for light slides, the file for dark slides, and whether the dark one is drawn in the
// corner of title, divider and close slides.
public sealed record ManifestLogos
{
    public string? Light { get; init; }
    public string? Dark { get; init; }
    public required bool ShowOnDark { get; init; }
}
