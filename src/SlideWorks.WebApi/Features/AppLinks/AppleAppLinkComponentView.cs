using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.AppLinks;

public sealed record AppleAppLinkComponentView([property: JsonPropertyName("/")] string Path);
