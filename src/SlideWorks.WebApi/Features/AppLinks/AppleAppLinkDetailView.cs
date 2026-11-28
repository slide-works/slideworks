using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.AppLinks;

public sealed record AppleAppLinkDetailView(
    [property: JsonPropertyName("appIDs")] IReadOnlyList<string> AppIds,
    [property: JsonPropertyName("components")] IReadOnlyList<AppleAppLinkComponentView> Components);
