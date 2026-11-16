using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;

namespace SlideWorks.WebApi.Features.Accounts;

// The signed-in person, pending or approved (SignedInPolicy), so a pending person's app can send them to /pending.
// Signed out, the policy answers 401 before this runs.
[Route("api/me")]
public class MeController(MeViews views) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = SignedInPolicy.NAME)]
    [ProducesResponseType<MeView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public Task<MeView> Get() => views.GetAsync();
}
