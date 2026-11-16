using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// RFC 6749 section 5.2.
public sealed record OAuthTokenErrorView(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);
