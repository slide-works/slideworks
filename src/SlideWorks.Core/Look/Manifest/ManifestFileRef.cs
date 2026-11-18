namespace SlideWorks.Core.Look.Manifest;

// One file field of a manifest: its path (such as fonts[1].file), its kind (a ManifestFiles constant) and its value,
// an asset id or a path in a bundle.
public sealed record ManifestFileRef(string Path, string Kind, string Value);
