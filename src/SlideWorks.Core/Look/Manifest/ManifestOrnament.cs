namespace SlideWorks.Core.Look.Manifest;

// A manifest's ornament: the palette token name of each dot, in order.
public sealed record ManifestOrnament
{
    public required IReadOnlyList<string> Dots { get; init; }
}
