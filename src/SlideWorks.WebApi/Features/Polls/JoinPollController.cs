using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// A guest's poll routes (V5). Anonymous, and they need the sw_guest cookie: without it, 400 no_guest. A code with no session
// is 404 no_session and a miss on the join-code limit; over the limit, 429 too_many_attempts (GuestPolls). The answer's body
// is read from the request by AnswerBody, at most AnswerBody.MAX_BYTES of it (else 413), and its other bodies come from
// AnswerResults.
[Route("api/join/{code}/poll")]
[AllowAnonymous]
public class JoinPollController(GuestPolls polls) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<GuestPollView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public Task<GuestPollView> Get(string code) => polls.ViewAsync(code, GuestCookie.From(Request), HttpContext.Connection.RemoteIpAddress);

    [HttpPut("answer")]
    [ProducesResponseType<MyAnswer>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<StaleAnswer>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Answer(string code)
        => AnswerResults.ToActionResult(await polls.SubmitAsync(code, GuestCookie.From(Request), Request.Body, HttpContext.Connection.RemoteIpAddress));
}
