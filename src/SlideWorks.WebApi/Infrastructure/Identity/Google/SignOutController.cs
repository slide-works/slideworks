using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// POST /api/signout: ends the cookie session. Under /api, so CrossSiteWriteGuard refuses one another site sends.
// TODO: needs a signed-in person through the fallback policy until SignedInPolicy lands; it then carries
// [Authorize(Policy = SignedInPolicy.NAME)] so a pending person can sign out too.
[Route("api/signout")]
public class SignOutController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<NoContentResult> Post() => NoContentAfter(HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme));
}
