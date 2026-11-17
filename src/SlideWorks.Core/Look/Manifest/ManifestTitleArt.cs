namespace SlideWorks.Core.Look.Manifest;

// A manifest's title art. Kind is "none", "generator" (Generator names a built-in one) or "image" (File is the upload).
public sealed record ManifestTitleArt
{
    public required string Kind { get; init; }
    public string? Generator { get; init; }
    public string? File { get; init; }
}
