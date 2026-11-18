namespace SlideWorks.WebApi.Features.Templates.Assets;

// A file that has arrived and is not yet stored: its temp file, its size in bytes and its SHA-256 in lower-case hex.
public sealed record TempAsset(string Path, long Size, string Sha256);
