using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// GET /signin/google?returnTo=<local path>: 302 to Google (anonymous; 503 while sign-in isn't set up). The sign-in page's
// button is a plain link here. Google comes back to GoogleSignInSetup.CALLBACK_PATH, which the handler answers, and the
// person lands on returnTo, or home when it isn't a local path (ReturnTo).
[Route("signin/google")]
public class GoogleSignInController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [RequiresGoogleSignIn]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Start([FromQuery] string? returnTo)
        => Challenge(new AuthenticationProperties { RedirectUri = ReturnTo.LocalOrHome(returnTo) }, GoogleSignInSetup.SCHEME);
}
