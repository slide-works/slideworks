using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// 503 with { "error": ... } when sign-in with Google isn't set up on this server, before the action runs.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresGoogleSignInAttribute : ActionFilterAttribute
{
    public const string NOT_SET_UP = "Sign-in isn't set up on this server";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.HttpContext.RequestServices.GetRequiredService<GoogleSignInSettings>().IsConfigured)
        {
            context.Result = new ObjectResult(new ErrorResponse(NOT_SET_UP)) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }
}
