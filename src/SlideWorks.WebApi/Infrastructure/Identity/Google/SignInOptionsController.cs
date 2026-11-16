using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// The sign-in page's one request. Anonymous: whoever is on /signin is signed out.
[Route("api/signin/options")]
public class SignInOptionsController(GoogleSignInSettings settings) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType<SignInOptionsView>(StatusCodes.Status200OK)]
    public SignInOptionsView Get() => new(settings.IsConfigured);
}
