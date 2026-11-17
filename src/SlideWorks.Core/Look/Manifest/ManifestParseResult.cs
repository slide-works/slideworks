namespace SlideWorks.Core.Look.Manifest;

// What ManifestJson.Parse returns: the manifest, or what is wrong with it. Exactly one of the two is set.
public sealed record ManifestParseResult
{
    public TemplateManifest? Manifest { get; init; }
    public required IReadOnlyList<ManifestError> Errors { get; init; }
}
