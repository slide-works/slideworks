using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.OAuth.Stores;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// The RFC 7591 registration response for a public client: no secret, PKCE at the token endpoint instead.
public sealed record ClientRegistrationView(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string> RedirectUris,
    [property: JsonPropertyName("client_id_issued_at")] long ClientIdIssuedAt,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
    [property: JsonPropertyName("grant_types")] IReadOnlyList<string> GrantTypes,
    [property: JsonPropertyName("response_types")] IReadOnlyList<string> ResponseTypes)
{
    public static ClientRegistrationView From(OAuthClient client)
        => new(client.ClientId, client.ClientName, client.RedirectUris, client.CreatedAt.ToUnixTimeSeconds(), "none", ["authorization_code", "refresh_token"], ["code"]);
}
