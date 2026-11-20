using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// upload_asset: the one-time address to PUT the file's bytes to, a ready curl line for it, the kind and how long the
// address stays open, in words.
public sealed record UploadAssetResult(
    [property: JsonPropertyName("upload_url")] string UploadUrl,
    string Curl,
    string Kind,
    string Expires);
