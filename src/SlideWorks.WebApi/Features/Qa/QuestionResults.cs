using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Qa;

// Maps what the guest seams return to the bodies of the guest Q&A and reaction routes (question-ask.json,
// question-votes.json, guest-questions.json "reaction", Q4, Q7). Each result is a closed set, so every case has a body.
public static class QuestionResults
{
    public static IActionResult ToActionResult(AskResult result) => result switch
    {
        AskResult.Asked asked => new ObjectResult(new AskedQuestion(asked.Question, asked.Mine)) { StatusCode = StatusCodes.Status201Created },
        AskResult.Invalid invalid => new BadRequestObjectResult(new ErrorResponse(invalid.Message)),
        AskResult.Refused refused => new ConflictObjectResult(new ErrorResponse(refused.Error)),
        AskResult.Limited limited => new ObjectResult(new TooManyQuestions(limited.RetryAfter)) { StatusCode = StatusCodes.Status429TooManyRequests },
        AskResult.NoSession => NoSession(),
        _ => throw new InvalidOperationException($"AskResult has no case {result.GetType().Name}."),
    };

    public static IActionResult ToActionResult(VoteResult result) => result switch
    {
        VoteResult.Voted voted => new OkObjectResult(new MyVotes(voted.Picks, voted.Revision)),
        VoteResult.Stale stale => new ConflictObjectResult(new StaleVotes(stale.Current)),
        VoteResult.Invalid invalid => new BadRequestObjectResult(new ErrorResponse(invalid.Message)),
        VoteResult.Refused refused => new ConflictObjectResult(new ErrorResponse(refused.Error)),
        VoteResult.Limited => new ObjectResult(new ErrorResponse(GuestQaErrors.TOO_MANY_VOTES)) { StatusCode = StatusCodes.Status429TooManyRequests },
        VoteResult.NoSession => NoSession(),
        _ => throw new InvalidOperationException($"VoteResult has no case {result.GetType().Name}."),
    };

    public static IActionResult ToActionResult(ReactionResult result) => result switch
    {
        ReactionResult.Counted => new NoContentResult(),
        ReactionResult.Invalid invalid => new BadRequestObjectResult(new ErrorResponse(invalid.Message)),
        ReactionResult.Refused refused => new ConflictObjectResult(new ErrorResponse(refused.Error)),
        ReactionResult.Limited => new ObjectResult(new ErrorResponse(GuestQaErrors.TOO_MANY_REACTIONS)) { StatusCode = StatusCodes.Status429TooManyRequests },
        ReactionResult.NoSession => NoSession(),
        _ => throw new InvalidOperationException($"ReactionResult has no case {result.GetType().Name}."),
    };

    private static NotFoundObjectResult NoSession() => new(new ErrorResponse(GuestViews.NO_SESSION));
}
