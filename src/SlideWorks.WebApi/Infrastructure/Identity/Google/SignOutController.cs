using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// POST /api/signout: ends the cookie session. Under /api, so CrossSiteWriteGuard refuses one another site sends.
// SignedInPolicy, so a pending person can sign out too.
[Route("api/signout")]
public class SignOutController : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = SignedInPolicy.NAME)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<NoContentResult> Post() => NoContentAfter(HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme));
}
