using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// Maps ConsentGoneException to 410 { error }, so the consent actions stay one line.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ConsentGoneAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not ConsentGoneException gone)
        {
            return;
        }
        context.Result = new ObjectResult(new ErrorResponse(gone.Message)) { StatusCode = StatusCodes.Status410Gone };
        context.ExceptionHandled = true;
    }
}
