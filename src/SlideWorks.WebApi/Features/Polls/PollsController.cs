using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// The presenter's poll routes (V4). Slide is the 1-based slide number. A person who can present the deck only (PollAccess):
// 404 for a deck the caller can't view or a slide that is not a poll, 403 for one they can view but not present. A refused
// action is 409 { "error": code } with a PollRefusals code.
[Route("api/decks/{id}/polls/{slide:int:min(1)}")]
public class PollsController(PollAccess access, IPolls polls) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PollControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<PollControlView> Get(string id, int slide) => access.ReadAsync(id, () => polls.ControlAsync(id, slide));

    [HttpPost("open")]
    [ProducesResponseType<PollControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<PollControlView> Open(string id, int slide) => access.RunAsync(id, () => polls.OpenAsync(id, slide));

    [HttpPost("close")]
    [ProducesResponseType<PollControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<PollControlView> Close(string id, int slide) => access.RunAsync(id, () => polls.CloseAsync(id, slide));

    [HttpPost("reveal")]
    [ProducesResponseType<PollControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<PollControlView> Reveal(string id, int slide) => access.RunAsync(id, () => polls.RevealAsync(id, slide));

    [HttpPost("answer")]
    [ProducesResponseType<PollControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<PollControlView> ShowAnswer(string id, int slide) => access.RunAsync(id, () => polls.ShowAnswerAsync(id, slide));

    [HttpPost("round")]
    [ProducesResponseType<PollControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<PollControlView> NewRound(string id, int slide) => access.RunAsync(id, () => polls.NewRoundAsync(id, slide));
}
