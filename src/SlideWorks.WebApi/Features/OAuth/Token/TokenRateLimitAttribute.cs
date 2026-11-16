using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// Refuses a caller over TokenLimiter's budget with 429, Retry-After in whole seconds and an RFC 6749 error body, before
// the form is read or the database is touched. Ordered after NoStore, so the refusal is no-store too.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TokenRateLimitAttribute : Attribute, IResourceFilter, IOrderedFilter
{
    public int Order => 1;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var limiter = context.HttpContext.RequestServices.GetRequiredService<TokenLimiter>();
        var callerKey = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (limiter.TryAcquire(callerKey, out var retryAfter))
        {
            return;
        }
        context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        context.Result = new ObjectResult(new OAuthTokenErrorView("too_many_requests", $"At most {TokenLimiter.PER_MINUTE} token and revoke requests a minute from one address. Try again shortly."))
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
        };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        // Nothing to do after: a refusal was decided before the action.
    }
}
