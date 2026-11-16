using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OAuth.Metadata;

// RFC 9728 protected resource metadata for /mcp.
public sealed record ProtectedResourceMetadataView(
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("authorization_servers")] IReadOnlyList<string> AuthorizationServers,
    [property: JsonPropertyName("scopes_supported")] IReadOnlyList<string> ScopesSupported,
    [property: JsonPropertyName("bearer_methods_supported")] IReadOnlyList<string> BearerMethodsSupported)
{
    public static ProtectedResourceMetadataView From(string publicBase, string mcp)
        => new(mcp, [publicBase], [OAuthScopes.DECKS], ["header"]);
}
