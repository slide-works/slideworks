namespace SlideWorks.WebApi.Features.Images;

// An uploaded image the caller may see: its file, its stored type and its entity tag (quoted).
public sealed record StoredImage(string Path, string ContentType, string ETag);
