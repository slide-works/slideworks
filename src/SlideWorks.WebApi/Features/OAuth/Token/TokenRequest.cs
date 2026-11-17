using Microsoft.AspNetCore.Mvc;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// The form parameters of POST /oauth/token that SlideWorks reads, for both grants. Any other parameter is ignored. The
// body is application/x-www-form-urlencoded only, and no parameter may repeat: FormEncodedOnly refuses anything else with
// invalid_request before these are bound.
public sealed class TokenRequest
{
    [FromForm(Name = "grant_type")]
    public string? GrantType { get; init; }

    [FromForm(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromForm(Name = "code")]
    public string? Code { get; init; }

    [FromForm(Name = "redirect_uri")]
    public string? RedirectUri { get; init; }

    [FromForm(Name = "code_verifier")]
    public string? CodeVerifier { get; init; }

    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; init; }

    [FromForm(Name = "resource")]
    public string? Resource { get; init; }
}
