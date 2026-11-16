using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// POST /oauth/token (RFC 6749 section 3.2) and POST /oauth/revoke (RFC 7009). Anonymous: an agent calls them with no
// cookie, and its form parameters are its credentials. Outside /api, so outside the cross-site write guard. Every
// response, error or not, carries Cache-Control: no-store.
[AllowAnonymous]
[NoStore]
[OAuthTokenError]
[Route("oauth")]
public class OAuthTokenController(TokenGrants grants) : ApiControllerBase
{
    [HttpPost("token")]
    [ProducesResponseType<TokenResponseView>(StatusCodes.Status200OK)]
    [ProducesResponseType<OAuthTokenErrorView>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<OAuthTokenErrorView>(StatusCodes.Status401Unauthorized)]
    public Task<TokenResponseView> Token([FromForm] TokenRequest request) => grants.ExchangeAsync(request);

    // Always 200 for any token, known or not (RFC 7009 section 2.2), with an empty body.
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OAuthTokenErrorView>(StatusCodes.Status400BadRequest)]
    public Task Revoke([FromForm] RevokeRequest request) => grants.RevokeAsync(request);
}
