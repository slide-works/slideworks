using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;

namespace SlideWorks.WebApi.Tests.Identity.Gate;

// Test-only, and only on hosts that add it with AddTo: one endpoint behind a bare [Authorize] (the default policy) and
// one behind the Admin policy, so the gate is tested on them before any product endpoint uses them. Each answers 204.
[ApiController]
[Route(PATH)]
public class GateProbeController : ControllerBase
{
    public const string PATH = "test/gate-probe";

    public const string BARE_PATH = PATH + "/bare";

    public const string ADMIN_PATH = PATH + "/admin";

    // For ConfigureTestServices: makes this test assembly's controllers part of the app.
    public static void AddTo(IServiceCollection services)
        => services.AddControllers().AddApplicationPart(typeof(GateProbeController).Assembly);

    [Authorize]
    [HttpGet("bare")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public NoContentResult Bare() => NoContent();

    [Authorize(Policy = AdminPolicy.NAME)]
    [HttpGet("admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public NoContentResult Admin() => NoContent();
}
