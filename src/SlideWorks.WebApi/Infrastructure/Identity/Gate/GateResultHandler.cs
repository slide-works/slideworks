using System.Net.Mime;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace SlideWorks.WebApi.Infrastructure.Identity.Gate;

// Writes the 403 body of a signed-in caller refused by the approval gate (S3): the Error of the policy's first
// IGateRequirement that failed, such as { "error": "pending_approval" }. Every other outcome (allowed, a challenge, any
// other refusal) is the framework's.
[RegisterSingleton(For = typeof(IAuthorizationMiddlewareResultHandler))]
public sealed class GateResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _framework = new();

    // AG0003 is about passing the whole HttpContext into helpers; this signature is the framework's.
#pragma warning disable AG0003
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
#pragma warning restore AG0003
    {
        var failed = authorizeResult.AuthorizationFailure?.FailedRequirements;
        var gate = authorizeResult.Forbidden && failed != null
            ? policy.Requirements.OfType<IGateRequirement>().FirstOrDefault(failed.Contains)
            : null;
        if (gate == null)
        {
            return _framework.HandleAsync(next, context, policy, authorizeResult);
        }
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = MediaTypeNames.Application.Json;
        return context.Response.WriteAsJsonAsync(new ErrorResponse(gate.Error), context.RequestAborted);
    }
}
