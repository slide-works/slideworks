using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

public sealed record OAuthRegistrationErrorView(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);
