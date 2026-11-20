namespace SlideWorks.WebApi.Features.Templates.Assets;

// A stored file of a template that the caller may read: where it is, the type its check gave it and its ETag.
public sealed record AssetFile(string Path, string ContentType, string ETag);
