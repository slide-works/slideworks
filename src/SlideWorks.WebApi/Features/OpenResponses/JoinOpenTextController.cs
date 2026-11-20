using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OpenResponses;

// A guest's phone on an open-text slide (D13): PUT its answer. Open to anonymous callers, as the join code is the only
// credential. A submit with no sw_guest cookie is 404 no_session, the same as a code with no session.
[Route("api/join/{code}/slides/{slide:int}")]
[AllowAnonymous]
public class JoinOpenTextController(OpenAnswers answers, OpenResponseLimiter limiter) : ApiControllerBase
{
    [HttpPut("answer")]
    [ProducesResponseType<StoredText>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public Task<StoredText> Put(string code, int slide, TextRequest request)
    {
        var guestId = GuestCookie.From(Request) ?? throw new NotFoundException(GuestViews.NO_SESSION);
        limiter.Take(guestId, code, slide);
        return answers.SubmitAsync(code, slide, guestId, request);
    }
}
