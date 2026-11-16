using System.Security.Claims;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Identity;

[RegisterPerRequest(For = typeof(ICurrentActor))]
public class HttpCurrentActor : ICurrentActor
{
    private readonly ClaimsPrincipal _principal;

    // AG0009 is about passing the whole HttpContext into helpers. The accessor is how a per-request service reaches the
    // request's principal, and only the principal is kept.
#pragma warning disable AG0009
    public HttpCurrentActor(IHttpContextAccessor httpContextAccessor)
#pragma warning restore AG0009
    {
        _principal = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
    }

    public bool IsSignedIn => PresenterId != null;

    public Guid? PresenterId => Guid.TryParse(_principal.FindFirstValue(SlideWorksClaims.PRESENTER_ID), out var id) ? id : null;

    public string? Email => IsSignedIn ? _principal.FindFirstValue(SlideWorksClaims.EMAIL) : null;

    public string? Name => IsSignedIn ? _principal.FindFirstValue(NameClaim) : null;

    public ActorKind Kind => Enum.TryParse<ActorKind>(_principal.FindFirstValue(SlideWorksClaims.ACTOR_KIND), out var kind) ? kind : ActorKind.Person;

    public ActorVia Via => Kind == ActorKind.Agent ? ActorVia.Mcp : ActorVia.Web;

    private string NameClaim => Kind == ActorKind.Agent ? SlideWorksClaims.AGENT_CLIENT_NAME : SlideWorksClaims.NAME;
}
