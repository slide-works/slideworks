using System.Security.Claims;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Identity;

[RegisterPerRequest(For = typeof(ICurrentActor))]
public class HttpCurrentActor : PrincipalActor
{
    // AG0009 is about passing the whole HttpContext into helpers. The accessor is how a per-request service reaches the
    // request's principal, and only the principal is kept.
#pragma warning disable AG0009
    public HttpCurrentActor(IHttpContextAccessor httpContextAccessor)
#pragma warning restore AG0009
        : base(httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal())
    {
    }
}
