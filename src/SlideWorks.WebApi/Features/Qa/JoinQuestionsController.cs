using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Qa;

// A guest's question routes (Q4). Anonymous, and they need the sw_guest cookie: without it, 400 no_guest. A code with no
// session is 404 no_session and a miss on the join-code limit; over the limit, 429 too_many_attempts (GuestQa). A body is
// read up to its limit (AskBody, VotesBody; else 413) and bound by the framework, and the seams' results become bodies
// through QuestionResults.
[Route("api/join/{code}/questions")]
[AllowAnonymous]
public class JoinQuestionsController(GuestQa guests) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<GuestQuestionsView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public Task<GuestQuestionsView> Get(string code) => guests.ViewAsync(code, GuestCookie.From(Request), HttpContext.Connection.RemoteIpAddress);

    [HttpPost]
    [BodyLimit(AskBody.MAX_BYTES)]
    [ProducesResponseType<AskedQuestion>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<TooManyQuestions>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Ask(string code, AskRequest request)
        => QuestionResults.ToActionResult(await guests.AskAsync(code, GuestCookie.From(Request), request, HttpContext.Connection.RemoteIpAddress));

    [HttpPut("votes")]
    [BodyLimit(VotesBody.MAX_BYTES)]
    [ProducesResponseType<MyVotes>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<StaleVotes>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Vote(string code, VotesBody body)
        => QuestionResults.ToActionResult(await guests.VoteAsync(code, GuestCookie.From(Request), body, HttpContext.Connection.RemoteIpAddress));
}
