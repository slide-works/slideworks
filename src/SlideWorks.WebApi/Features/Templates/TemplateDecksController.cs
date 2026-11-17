using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// "Update my decks" (D14): moves the caller's own decks to the template's latest version and answers how many moved. On
// the default policy, so a signed-out caller gets 401 and a pending person 403 pending_approval.
[Route("api/templates/{id}/update-my-decks")]
public class TemplateDecksController(IUpdateMyDecks decks, ICurrentActor actor) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<OkObjectResult> Post(string id) => Ok(new { count = await decks.RunAsync(actor, id, HttpContext.RequestAborted) });
}
