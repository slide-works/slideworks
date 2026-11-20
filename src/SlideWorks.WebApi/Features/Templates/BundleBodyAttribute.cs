using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Templates;

// On the bundle import, whose bundle is the request body itself, read as a stream by the service.
//
// A body that is not sent as application/zip is 415 before the service is called. [Consumes] can't say this here: it
// takes the action out of routing, and the request then falls to the /api fallback and answers a bare 404.
//
// A body over the action's RequestSizeLimit is found out by the server in the middle of the service's read, which then
// throws BadHttpRequestException with status 413. That becomes 413 with the file_too_large body. Every other exception
// is left to the other filters.
[AttributeUsage(AttributeTargets.Method)]
public sealed class BundleBodyAttribute : ExceptionFilterAttribute, IResourceFilter
{
    public const string ZIP = "application/zip";
    public const string LIMIT = "60 MB";
    public const string NOT_A_ZIP = "Send the bundle as the request body, with Content-Type application/zip";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var sent = MediaTypeHeaderValue.TryParse(context.HttpContext.Request.ContentType, out var contentType) ? contentType.MediaType.Value : null;
        if (!string.Equals(sent, ZIP, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new ErrorResponse(NOT_A_ZIP)) { StatusCode = StatusCodes.Status415UnsupportedMediaType };
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        // Nothing to do once the action has run.
    }

    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
        {
            return;
        }
        var message = TemplateErrorTexts.Format(TemplateErrorCodes.FILE_TOO_LARGE, ("limit", LIMIT));
        context.Result = new ObjectResult(new TemplateErrorResponse(TemplateErrorCodes.FILE_TOO_LARGE, message, null, []))
        {
            StatusCode = StatusCodes.Status413PayloadTooLarge,
        };
        context.ExceptionHandled = true;
    }
}
