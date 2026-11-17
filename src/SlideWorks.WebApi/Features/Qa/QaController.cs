using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Qa;

// The presenter's Q&A routes (Q6), each answering QaControlView. A person who can present the deck only (QaAccess): 404 for
// a deck the caller can't view, 403 for one they can view but not present. Number is a question's 1-based number in the open
// round, slide a 1-based slide number. A refused action is 409 { "error": code } (not_live, wrong_state) and an unknown
// number 404, both from IQuestionModeration. The settings routes work live or not: they change through IQaSettings, then
// answer IQuestionModeration.ControlAsync. Routes never publish or record activity; the seams do.
[Route("api/decks/{id}/qa")]
public class QaController(QaAccess access, IQuestionModeration moderation, IQaSettings settings) : ApiControllerBase
{
    private const string QUESTION = "questions/{number:int:min(1)}";

    [HttpGet]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<QaControlView> Get(string id) => access.RunAsync(id, () => moderation.ControlAsync(id));

    [HttpPost(QUESTION + "/approve")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<QaControlView> Approve(string id, int number) => access.RunAsync(id, () => moderation.ApproveAsync(id, number));

    [HttpPost(QUESTION + "/hide")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<QaControlView> Hide(string id, int number) => access.RunAsync(id, () => moderation.HideAsync(id, number));

    [HttpPost(QUESTION + "/restore")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<QaControlView> Restore(string id, int number) => access.RunAsync(id, () => moderation.RestoreAsync(id, number));

    [HttpPost(QUESTION + "/answer")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<QaControlView> Answer(string id, int number) => access.RunAsync(id, () => moderation.AnswerAsync(id, number));

    [HttpPost(QUESTION + "/reopen")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<QaControlView> Reopen(string id, int number) => access.RunAsync(id, () => moderation.ReopenAsync(id, number));

    [HttpPost(QUESTION + "/show")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<QaControlView> Show(string id, int number) => access.RunAsync(id, () => moderation.ShowAsync(id, number));

    [HttpPost(QUESTION + "/unshow")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<QaControlView> Unshow(string id, int number) => access.RunAsync(id, () => moderation.UnshowAsync(id, number));

    [HttpPut("settings")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<QaControlView> PutSettings(string id, QaSettingsPatch patch) => access.ChangeAsync(id, () => settings.UpdateAsync(id, patch));

    [HttpPut("slides/{slide:int:min(1)}")]
    [ProducesResponseType<QaControlView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<QaControlView> PutSlide(string id, int slide, QaSlidePatch patch) => access.ChangeAsync(id, () => settings.SetSlideAsync(id, slide, patch));
}
