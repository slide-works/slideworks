using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Dtos;

namespace SlideWorks.WebApi.Tests.Identity;

// Test-only, and only on hosts that add it with AddTo: issues a real session cookie through the app's Cookies scheme
// (no sign-in endpoint exists until Google sign-in lands) and answers with the scheme and host the app saw. It runs as
// an endpoint, so after every middleware in UseSlideWorksAuth, exactly where a real sign-in callback would.
[AllowAnonymous]
[ApiController]
[Route(PATH)]
public class SessionProbeController : ControllerBase
{
    public const string PATH = "test/session-probe";

    // For ConfigureTestServices: makes this test assembly's controller part of the app.
    public static void AddTo(IServiceCollection services)
        => services.AddControllers().AddApplicationPart(typeof(SessionProbeController).Assembly);

    [HttpGet]
    [ProducesResponseType<RequestOriginDto>(StatusCodes.Status200OK)]
    public async Task<RequestOriginDto> SignIn()
    {
        var identity = new ClaimsIdentity([new Claim(SlideWorksClaims.EMAIL, "probe@example.test")], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return new RequestOriginDto(Request.Scheme, Request.Host.Value!);
    }
}
