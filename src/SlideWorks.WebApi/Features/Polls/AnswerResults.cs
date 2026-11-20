using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// Maps what IPollAnswers.SubmitAsync returns to the bodies of PUT /api/join/{code}/poll/answer (poll-answer.json, V5).
public static class AnswerResults
{
    public const string TOO_MANY_ANSWERS = "too_many_answers";

    public static IActionResult ToActionResult(AnswerResult result) => result switch
    {
        AnswerResult.Accepted accepted => new OkObjectResult(new MyAnswer(accepted.Picks, accepted.Revision, accepted.Text)),
        AnswerResult.Stale stale => new ConflictObjectResult(new StaleAnswer(stale.Current)),
        AnswerResult.Invalid invalid => new BadRequestObjectResult(new ErrorResponse(invalid.Message)),
        AnswerResult.Limited => new ObjectResult(new ErrorResponse(TOO_MANY_ANSWERS)) { StatusCode = StatusCodes.Status429TooManyRequests },
        AnswerResult.NoSession => new NotFoundObjectResult(new ErrorResponse(GuestViews.NO_SESSION)),
        _ => throw new InvalidOperationException($"AnswerResult has no case {result.GetType().Name}."),
    };
}
