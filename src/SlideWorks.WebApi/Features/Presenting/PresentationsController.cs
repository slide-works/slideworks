using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Presenting.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Presenting;

// The phone's pick page (presentations.json, P4): the signed-in person's live presentations. Signed out, the fallback
// policy answers 401 before this runs.
[Route("api/presentations")]
public class PresentationsController(PresenterQueries queries) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<LivePresentationsView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public Task<LivePresentationsView> List() => queries.LiveAsync();
}
