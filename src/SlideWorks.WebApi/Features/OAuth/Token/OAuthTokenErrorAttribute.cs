using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// Maps OAuthTokenException to its status and { error, error_description }, so the actions stay one line.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OAuthTokenErrorAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not OAuthTokenException refused)
        {
            return;
        }
        context.Result = new ObjectResult(new OAuthTokenErrorView(refused.Error, refused.Message)) { StatusCode = refused.Status };
        context.ExceptionHandled = true;
    }
}
