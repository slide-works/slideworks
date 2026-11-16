using Microsoft.AspNetCore.Mvc;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The query of GET /oauth/authorize (RFC 6749 4.1.1, RFC 7636 4.3, RFC 8707). Every field may be missing; the handler
// decides what a missing one means.
public sealed record AuthorizeQuery
{
    [FromQuery(Name = "response_type")]
    public string? ResponseType { get; init; }

    [FromQuery(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromQuery(Name = "redirect_uri")]
    public string? RedirectUri { get; init; }

    [FromQuery(Name = "state")]
    public string? State { get; init; }

    [FromQuery(Name = "code_challenge")]
    public string? CodeChallenge { get; init; }

    [FromQuery(Name = "code_challenge_method")]
    public string? CodeChallengeMethod { get; init; }

    [FromQuery(Name = "scope")]
    public string? Scope { get; init; }

    [FromQuery(Name = "resource")]
    public string? Resource { get; init; }
}
