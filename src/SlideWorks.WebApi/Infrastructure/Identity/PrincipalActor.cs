using System.Security.Claims;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// The claims-to-actor rules (K1) over any principal. HttpCurrentActor reads the request's; DeckHub reads its own connection's
// (Context.User), because under long polling a hub call runs after the request that opened it has finished.
public class PrincipalActor(ClaimsPrincipal principal) : ICurrentActor
{
    private readonly ClaimsPrincipal _principal = principal;

    public bool IsSignedIn => PresenterId != null;

    public Guid? PresenterId => Guid.TryParse(_principal.FindFirstValue(SlideWorksClaims.PRESENTER_ID), out var id) ? id : null;

    public string? Email => IsSignedIn ? _principal.FindFirstValue(SlideWorksClaims.EMAIL) : null;

    public string? Name => IsSignedIn ? _principal.FindFirstValue(NameClaim) : null;

    public ActorKind Kind => Enum.TryParse<ActorKind>(_principal.FindFirstValue(SlideWorksClaims.ACTOR_KIND), out var kind) ? kind : ActorKind.Person;

    public ActorVia Via => Kind == ActorKind.Agent ? ActorVia.Mcp : ActorVia.Web;

    public UserAccess? Access
    {
        get
        {
            if (!IsSignedIn)
            {
                return null;
            }
            if (Kind == ActorKind.Agent)
            {
                return UserAccess.Approved;
            }
            return Enum.TryParse<UserAccess>(_principal.FindFirstValue(SlideWorksClaims.ACCESS), out var access) ? access : null;
        }
    }

    public bool IsAdmin => IsSignedIn && Kind == ActorKind.Person && _principal.FindFirstValue(SlideWorksClaims.IS_ADMIN) == SessionPrincipal.IS_ADMIN_VALUE;

    private string NameClaim => Kind == ActorKind.Agent ? SlideWorksClaims.AGENT_CLIENT_NAME : SlideWorksClaims.NAME;
}
