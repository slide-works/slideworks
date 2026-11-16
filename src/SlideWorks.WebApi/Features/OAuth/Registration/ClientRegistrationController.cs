using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// POST /oauth/register. Anonymous: an agent registers before anyone has signed in. Outside /api, so outside the
// cross-site write guard: it takes no cookie and changes nothing a person owns.
[AllowAnonymous]
[OAuthRegistrationError]
[Route("oauth/register")]
public class ClientRegistrationController(ClientRegistrar registrar) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<ClientRegistrationView>(StatusCodes.Status201Created)]
    [ProducesResponseType<OAuthRegistrationErrorView>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<OAuthRegistrationErrorView>(StatusCodes.Status429TooManyRequests)]
    public async Task<CreatedResult> Register([FromBody] ClientRegistrationRequest request)
        => Created((string?)null, await registrar.RegisterAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
}
