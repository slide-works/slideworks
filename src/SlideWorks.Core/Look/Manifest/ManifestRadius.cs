namespace SlideWorks.Core.Look.Manifest;

// A manifest's corner radii in px.
public sealed record ManifestRadius
{
    public required int Sm { get; init; }
    public required int Md { get; init; }
    public required int Lg { get; init; }
    public required int Xl { get; init; }
}
