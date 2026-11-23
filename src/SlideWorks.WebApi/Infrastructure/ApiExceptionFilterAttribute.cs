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
            ForbiddenException e => new ObjectResult(new ErrorResponse(e.Message)) { StatusCode = StatusCodes.Status403Forbidden },
            BadRequestException e => new BadRequestObjectResult(new ErrorResponse(e.Message)),
            ConflictException e => new ConflictObjectResult(new ErrorResponse(e.Message)),
            TooManyRequestsException e => new ObjectResult(new ErrorResponse(e.Message)) { StatusCode = StatusCodes.Status429TooManyRequests },
            PayloadTooLargeException e => new ObjectResult(new ErrorResponse(e.Message)) { StatusCode = StatusCodes.Status413PayloadTooLarge },
            _ => null,
        };
        if (result == null)
        {
            return;
        }
        if (context.Exception is TooManyRequestsException { RetryAfter: { } retryAfter })
        {
            context.HttpContext.Response.Headers.RetryAfter = ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        context.Result = result;
        context.ExceptionHandled = true;
    }
}
