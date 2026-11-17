using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// A guest's poll routes (V5). Anonymous, and they need the sw_guest cookie: without it, 400 no_guest. A code with no session
// is 404 no_session. The answer's body is read by AnswerBody, and its other bodies come from AnswerResults.
[Route("api/join/{code}/poll")]
[AllowAnonymous]
public class JoinPollController(GuestPolls polls) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<GuestPollView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<GuestPollView> Get(string code) => polls.ViewAsync(code, GuestCookie.From(Request));

    [HttpPut("answer")]
    [ProducesResponseType<MyAnswer>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<StaleAnswer>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Answer(string code, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonElement body)
        => AnswerResults.ToActionResult(await polls.SubmitAsync(code, GuestCookie.From(Request), body));
}
