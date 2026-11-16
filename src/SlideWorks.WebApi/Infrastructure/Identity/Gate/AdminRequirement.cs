using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace SlideWorks.WebApi.Infrastructure.Identity.Gate;

// A person who is an admin (S3). AdminPolicy puts it after ApprovedRequirement, so a pending person still hears
// pending_approval rather than this. It handles itself, so it needs no registration.
public sealed class AdminRequirement : AuthorizationHandler<AdminRequirement>, IGateRequirement
{
    public const string ERROR = "Admins only";

    public string Error => ERROR;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        if (context.User.FindFirstValue(SlideWorksClaims.IS_ADMIN) == SessionPrincipal.IS_ADMIN_VALUE)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
