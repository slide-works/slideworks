using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// Maps OAuthRegistrationException to its status and { error, error_description }, so the action stays one line.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OAuthRegistrationErrorAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not OAuthRegistrationException refused)
        {
            return;
        }
        context.Result = new ObjectResult(new OAuthRegistrationErrorView(refused.Error, refused.Message)) { StatusCode = refused.Status };
        context.ExceptionHandled = true;
    }
}
