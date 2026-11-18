namespace SlideWorks.Core.Look.Manifest;

// One thing wrong with a manifest: where (such as fonts[1].weight, or empty for the whole file) and what.
public sealed record ManifestError(string Path, string Message);
