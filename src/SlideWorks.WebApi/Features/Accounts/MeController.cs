using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts;

// The signed-in person. Signed out, the fallback policy answers 401 before this runs.
[Route("api/me")]
public class MeController(IUserDirectory users, ICurrentActor actor) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<MeView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<MeView> Get()
    {
        var user = await users.FindByEmailAsync(actor.Email!) ?? throw new NotFoundException("The signed-in person no longer exists.");
        // TODO: the real access, admin, pending count and admin-missing values come from IUserAccounts.
        return new MeView(user.Email, user.Name, user.PictureUrl, "approved", false, 0, false);
    }
}
