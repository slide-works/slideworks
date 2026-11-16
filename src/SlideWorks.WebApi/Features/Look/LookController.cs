using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Look;

// Changes a deck's look from the Look panel (L6, L8). The deck's owner only; anyone else gets 404.
[Route("api/decks/{id}/look")]
public class LookController(LookCommands commands) : ApiControllerBase
{
    [HttpPut]
    [ProducesResponseType<LookChangeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<LookChangeResult> Put(string id, LookChangeRequest request) => commands.SetAsync(id, request.ToChange(), tool: null);
}
