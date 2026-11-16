using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity.Gate;

// A person whose access is Approved (S3). The fallback and default policies carry it, so a pending person gets 403
// pending_approval everywhere except AllowAnonymous routes and the SignedIn policy's. It handles itself, so it needs no
// registration.
public sealed class ApprovedRequirement : AuthorizationHandler<ApprovedRequirement>, IGateRequirement
{
    public const string ERROR = "pending_approval";

    public string Error => ERROR;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApprovedRequirement requirement)
    {
        if (context.User.FindFirstValue(SlideWorksClaims.ACCESS) == nameof(UserAccess.Approved))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
