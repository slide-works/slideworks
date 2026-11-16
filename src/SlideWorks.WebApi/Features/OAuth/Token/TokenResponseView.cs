using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// RFC 6749 section 5.1. TokenType is always Bearer; ExpiresIn is the access token's lifetime in seconds.
public sealed record TokenResponseView(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("scope")] string Scope);
