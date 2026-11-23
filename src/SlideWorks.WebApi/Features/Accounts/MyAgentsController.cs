using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Accounts.Profile;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Accounts;

// Disconnects one of the signed-in person's connected agents (C3); id is the token family id.
[Route("api/me/agents")]
public class MyAgentsController(ProfileActions actions) : ApiControllerBase
{
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Delete(Guid id) => NoContentAfter(actions.DisconnectAgentAsync(id));
}
