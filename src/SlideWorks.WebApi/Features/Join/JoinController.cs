using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Join;

// What a guest's phone reads to join a room (J3). Open to anonymous callers: a join code is the only credential, and a
// code with no session answers 404 no_session, the same for every reason.
[Route("api/join")]
public class JoinController(GuestViews views) : ApiControllerBase
{
    [HttpGet("{code}")]
    [AllowAnonymous]
    [SetsGuestCookie]
    [ProducesResponseType<GuestView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public Task<GuestView> Get(string code) => views.GetAsync(code, HttpContext.Connection.RemoteIpAddress);
}
