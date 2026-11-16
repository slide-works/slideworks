using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Tests.Identity.Approval;

// Test-only, and only on hosts that add it with AddTo: answers with what ICurrentActor says about the caller. The person
// route is anonymous, so it answers under any gate and shows a signed-out caller as signed out rather than refusing them;
// the agent route takes only an agent's bearer token.
[ApiController]
[Route(PATH)]
public class ActorProbeController(ICurrentActor actor) : ControllerBase
{
    public const string PATH = "test/actor-probe";

    public const string AGENT_PATH = PATH + "/agent";

    // For ConfigureTestServices: makes this test assembly's controllers part of the app.
    public static void AddTo(IServiceCollection services)
        => services.AddControllers().AddApplicationPart(typeof(ActorProbeController).Assembly);

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType<ActorDto>(StatusCodes.Status200OK)]
    public ActorDto Person() => Describe();

    [Authorize(Policy = AgentPolicy.NAME)]
    [HttpGet("agent")]
    [ProducesResponseType<ActorDto>(StatusCodes.Status200OK)]
    public ActorDto Agent() => Describe();

    private ActorDto Describe() => new(actor.IsSignedIn, actor.Email, actor.Kind.ToString(), actor.Access?.ToString(), actor.IsAdmin);
}
