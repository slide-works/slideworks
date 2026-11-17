using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SlideWorks.WebApi.Features.Templates.Views;

namespace SlideWorks.WebApi.Features.Templates;

// On the template controllers: a TemplateRefusedException becomes 400 with a TemplateErrorResponse, or 409 when its code
// is stale_revision. Every other exception is left to ApiExceptionFilterAttribute.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TemplateErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not TemplateRefusedException refused)
        {
            return;
        }
        context.Result = new ObjectResult(new TemplateErrorResponse(refused.Code, refused.Message, refused.Path, refused.Errors))
        {
            StatusCode = refused.Code == TemplateErrorCodes.STALE_REVISION ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
        };
        context.ExceptionHandled = true;
    }
}
