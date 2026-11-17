using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Qa;

// Reads at most maxBytes of a guest route's request body before model binding (Q4, Q7): a longer body is 413 { "error":
// "body: at most N bytes" } and the rest is never read, as AnswerBody does for a poll answer. A body within the limit is handed
// on to the framework's JSON binding unchanged, so a body that isn't JSON gets the framework's 400.
[AttributeUsage(AttributeTargets.Method)]
public sealed class BodyLimitAttribute(int maxBytes) : Attribute, IAsyncResourceFilter
{
    public int MaxBytes => maxBytes;

    public static string TooLarge(int maxBytes) => $"body: at most {maxBytes} bytes";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > maxBytes)
        {
            context.Result = TooLargeResult();
            return;
        }
        var buffer = new byte[(int)Math.Min(request.ContentLength ?? maxBytes, maxBytes) + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await request.Body.ReadAsync(buffer.AsMemory(length), context.HttpContext.RequestAborted)) > 0)
        {
            length += read;
        }
        if (length > maxBytes)
        {
            context.Result = TooLargeResult();
            return;
        }
        request.Body = new MemoryStream(buffer, 0, length, writable: false);
        await next();
    }

    private ObjectResult TooLargeResult() => new(new ErrorResponse(TooLarge(maxBytes))) { StatusCode = StatusCodes.Status413PayloadTooLarge };
}
