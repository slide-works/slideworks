using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// GET /oauth/authorize. Anonymous at the endpoint, because the agent's browser may not be signed in yet: the handler sends
// a signed-out browser to sign in and back. Answers are redirects, or a 400 page for an unknown client or redirect URI.
[AllowAnonymous]
[Route("oauth/authorize")]
public class OAuthAuthorizeController(AuthorizeHandler handler) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Authorize([FromQuery] AuthorizeQuery query) => handler.HandleAsync(query, Request.GetEncodedPathAndQuery());
}
