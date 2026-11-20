using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OpenResponses;

// The presenter's controls for a word-cloud or open-text slide (open-response-control.json). Results stay live (D14), so
// there is no reveal: hide and restore are the only way an entry leaves the screen.
[Route("api/decks/{id}/slides/{slide:int}/response")]
[ProducesResponseType<OpenResponseControlView>(StatusCodes.Status200OK)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
public class OpenResponseController(OpenResponseControls controls, OpenResponseModeration moderation) : ApiControllerBase
{
    [HttpGet]
    public Task<OpenResponseControlView> Get(string id, int slide) => controls.ViewAsync(id, slide);

    [HttpPost("open")]
    public Task<OpenResponseControlView> Open(string id, int slide) => controls.OpenAsync(id, slide);

    [HttpPost("close")]
    public Task<OpenResponseControlView> Close(string id, int slide) => controls.CloseAsync(id, slide);

    [HttpPost("new-round")]
    public Task<OpenResponseControlView> NewRound(string id, int slide) => controls.NewRoundAsync(id, slide);

    [HttpPost("entries/{number:int}/hide")]
    public Task<OpenResponseControlView> Hide(string id, int slide, int number) => moderation.HideAsync(id, slide, number);

    [HttpPost("entries/{number:int}/restore")]
    public Task<OpenResponseControlView> Restore(string id, int slide, int number) => moderation.RestoreAsync(id, slide, number);
}
