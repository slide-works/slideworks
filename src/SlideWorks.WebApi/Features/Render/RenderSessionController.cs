using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render;

// POST /api/render/{id}/session (L9): trades a render token in the X-Render-Token header for the sw_render image cookie.
// Anonymous (K1): the token is the only credential. A missing or invalid token is 404 "Deck not found" and sets nothing.
[Route("api/render")]
public class RenderSessionController(RenderSessions sessions) : ApiControllerBase
{
    [HttpPost("{id}/session")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public RenderSessionResult Start(string id, [FromHeader(Name = RenderSessions.HEADER)] string? token) => sessions.Start(id, token);
}
