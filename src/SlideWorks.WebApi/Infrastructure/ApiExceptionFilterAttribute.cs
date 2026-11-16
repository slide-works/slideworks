using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SlideWorks.WebApi.Infrastructure;

// Domain exceptions thrown by services map to status codes here, so controller
// actions stay one-line delegations with no try/catch.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        ObjectResult? result = context.Exception switch
        {
            NotFoundException e => new NotFoundObjectResult(new ErrorResponse(e.Message)),
            BadRequestException e => new BadRequestObjectResult(new ErrorResponse(e.Message)),
            ConflictException e => new ConflictObjectResult(new ErrorResponse(e.Message)),
            _ => null,
        };
        if (result == null)
        {
            return;
        }
        context.Result = result;
        context.ExceptionHandled = true;
    }
}
