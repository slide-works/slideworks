using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render.Print;

// A PDF that can't be made right now: 503 with the error's text, no-store, and Retry-After saying when to try again.
public sealed class PdfUnavailableResult(string error) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "no-store";
        headers[HeaderNames.RetryAfter] = CardUnavailableResult.RETRY_AFTER_SECONDS.ToString();
        return new ObjectResult(new ErrorResponse(error)) { StatusCode = StatusCodes.Status503ServiceUnavailable }
            .ExecuteResultAsync(context);
    }
}
