using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OpenResponses;

// A guest's phone on an open-text slide (D13): GET its view and PUT its answer. The same GET serves a word-cloud slide
// from its own reader, because both layouts share /response. Open to anonymous callers, as the join code is the only
// credential. A submit with no sw_guest cookie is 404 no_session, the same as a code with no session.
[Route("api/join/{code}/slides/{slide:int}")]
[AllowAnonymous]
public class JoinOpenTextController(OpenAnswers answers, WordSubmissions words) : ApiControllerBase
{
    [HttpGet("response")]
    [SetsGuestCookie]
    [ProducesResponseType<GuestResponseView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<GuestResponseView> Get(string code, int slide, CancellationToken cancellationToken)
    {
        var guestId = GuestCookie.From(Request);
        if (await answers.IsOpenTextAsync(code, slide, cancellationToken))
        {
            return await answers.ViewAsync(code, slide, guestId, cancellationToken);
        }
        return await words.ViewAsync(code, slide, guestId);
    }

    [HttpPut("answer")]
    [ProducesResponseType<StoredText>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public Task<StoredText> Put(string code, int slide, TextRequest request, CancellationToken cancellationToken)
    {
        var guestId = GuestCookie.From(Request) ?? throw new NotFoundException(GuestViews.NO_SESSION);
        return answers.SubmitAsync(code, slide, guestId, request, cancellationToken);
    }
}
