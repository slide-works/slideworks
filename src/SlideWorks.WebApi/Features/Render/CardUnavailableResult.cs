using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render;

// A share card that can't be rendered right now (the renderer is unavailable or timed out, SW-81 D5): 503 with the
// error's text. no-store keeps a CDN or a link previewer from keeping the error, and Retry-After says when to try again.
public sealed class CardUnavailableResult(string error) : IActionResult
{
    public const int RETRY_AFTER_SECONDS = 30;

    public Task ExecuteResultAsync(ActionContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "no-store";
        headers[HeaderNames.RetryAfter] = RETRY_AFTER_SECONDS.ToString();
        return new ObjectResult(new ErrorResponse(error)) { StatusCode = StatusCodes.Status503ServiceUnavailable }
            .ExecuteResultAsync(context);
    }
}
