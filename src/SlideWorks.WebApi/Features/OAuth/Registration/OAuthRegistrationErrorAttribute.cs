using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// Maps OAuthRegistrationException to its status and { error, error_description }, plus Retry-After in whole seconds when
// the refusal says when to come back, so the action stays one line.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OAuthRegistrationErrorAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not OAuthRegistrationException refused)
        {
            return;
        }
        if (refused.RetryAfter is { } retryAfter)
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }
        context.Result = new ObjectResult(new OAuthRegistrationErrorView(refused.Error, refused.Message)) { StatusCode = refused.Status };
        context.ExceptionHandled = true;
    }
}
