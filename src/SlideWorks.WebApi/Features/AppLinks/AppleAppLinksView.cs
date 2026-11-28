using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.AppLinks;

public sealed record AppleAppLinksView(
    [property: JsonPropertyName("apps")] IReadOnlyList<string> Apps,
    [property: JsonPropertyName("details")] IReadOnlyList<AppleAppLinkDetailView> Details);
