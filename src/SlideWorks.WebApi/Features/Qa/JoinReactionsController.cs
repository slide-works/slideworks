using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Qa;

// A guest's reaction tap (Q7): 204, or 400 emoji: unknown, 409 reactions_off, 429 too_many_reactions. Guest rules as the
// question routes (GuestQa). A tap's body is a few bytes, so at most MAX_BYTES of it is read (else 413).
[Route("api/join/{code}/reactions")]
[AllowAnonymous]
public class JoinReactionsController(GuestQa guests) : ApiControllerBase
{
    public const int MAX_BYTES = 256;

    [HttpPost]
    [BodyLimit(MAX_BYTES)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> React(string code, ReactionRequest request)
        => QuestionResults.ToActionResult(await guests.TapAsync(code, GuestCookie.From(Request), request, HttpContext.Connection.RemoteIpAddress));
}
