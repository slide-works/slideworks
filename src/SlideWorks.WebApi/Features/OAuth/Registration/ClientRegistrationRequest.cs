using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// The RFC 7591 fields SlideWorks reads. Any other metadata a client sends is ignored.
public sealed record ClientRegistrationRequest(
    [property: JsonPropertyName("client_name")] string? ClientName,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string>? RedirectUris);
