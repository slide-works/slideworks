namespace SlideWorks.Core.Look.Manifest;

// A manifest's shape: corner radii in px and the card shadow, which ManifestShadow has checked.
public sealed record ManifestShape
{
    public required ManifestRadius Radius { get; init; }
    public required string Shadow { get; init; }
}
