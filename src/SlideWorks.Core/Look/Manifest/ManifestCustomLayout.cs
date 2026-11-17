namespace SlideWorks.Core.Look.Manifest;

// A custom layout in a manifest: its name and its uploaded document {markup, css, slots}. The layout's surface, steps
// and slots live in that document.
public sealed record ManifestCustomLayout
{
    public required string Name { get; init; }
    public required string File { get; init; }
}
