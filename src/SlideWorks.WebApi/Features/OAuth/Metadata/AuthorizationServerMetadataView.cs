using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OAuth.Metadata;

// RFC 8414 authorization server metadata. Public clients only (no client secret), PKCE S256 only, and one scope.
public sealed record AuthorizationServerMetadataView(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("registration_endpoint")] string RegistrationEndpoint,
    [property: JsonPropertyName("revocation_endpoint")] string RevocationEndpoint,
    [property: JsonPropertyName("scopes_supported")] IReadOnlyList<string> ScopesSupported,
    [property: JsonPropertyName("response_types_supported")] IReadOnlyList<string> ResponseTypesSupported,
    [property: JsonPropertyName("grant_types_supported")] IReadOnlyList<string> GrantTypesSupported,
    [property: JsonPropertyName("code_challenge_methods_supported")] IReadOnlyList<string> CodeChallengeMethodsSupported,
    [property: JsonPropertyName("token_endpoint_auth_methods_supported")] IReadOnlyList<string> TokenEndpointAuthMethodsSupported,
    [property: JsonPropertyName("revocation_endpoint_auth_methods_supported")] IReadOnlyList<string> RevocationEndpointAuthMethodsSupported)
{
    public static AuthorizationServerMetadataView From(string issuer)
        => new(
            issuer,
            issuer + "/oauth/authorize",
            issuer + "/oauth/token",
            issuer + "/oauth/register",
            issuer + "/oauth/revoke",
            [OAuthScopes.DECKS],
            ["code"],
            ["authorization_code", "refresh_token"],
            ["S256"],
            ["none"],
            ["none"]);
}
