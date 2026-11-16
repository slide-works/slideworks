using Microsoft.AspNetCore.Authorization;

namespace SlideWorks.WebApi.Infrastructure.Identity.Gate;

// A requirement of the approval gate (S3) that names the 403 body a signed-in caller gets when it fails. GateResultHandler
// writes the Error of the first one that failed, in the policy's order.
public interface IGateRequirement : IAuthorizationRequirement
{
    string Error { get; }
}
