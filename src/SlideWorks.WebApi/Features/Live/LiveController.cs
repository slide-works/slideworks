using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Live;

// Go live, End and the live state of a deck (J2). The deck's owner only: a deck the caller can't see is 404, one they can
// see but don't own is 403.
[Route("api/decks/{id}/live")]
public class LiveController(LiveCommands commands) : ApiControllerBase
{
    // Anonymous so a signed-out or pending viewer of the player is answered by the owner check (404 or 403) and never meets
    // the global 401 or pending_approval redirects. It has no side effects, and POST and DELETE stay behind the gate.
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<LiveView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<LiveView> Get(string id) => commands.GetAsync(id);

    [HttpPost]
    [ProducesResponseType<LiveView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<LiveView> Post(string id) => commands.GoLiveAsync(id);

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Delete(string id) => NoContentAfter(commands.EndAsync(id));
}
