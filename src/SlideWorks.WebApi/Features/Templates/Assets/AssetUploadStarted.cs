namespace SlideWorks.WebApi.Features.Templates.Assets;

// The answer to starting an upload (T6): the one-time URL to PUT the file's bytes to, and how long it stays open, in
// words.
public sealed record AssetUploadStarted(string UploadUrl, string ExpiresLabel);
