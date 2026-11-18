using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// Sends a template's file (T6). Its type is the one its check stored, and only a type on AssetContentTypes' list;
// nosniff, so a browser never reads it as anything else. The Content-Security-Policy forbids every load and sandboxes
// the response, so an SVG (or a polyglot file) opened as a page runs no script and reaches no other address: it is only
// ever an image. The file is named by its content's hash and never changes, so it is immutable; private, because who may
// read it depends on the caller.
public sealed class AssetFileResult(AssetFile file) : IActionResult
{
    public const string CONTENT_SECURITY_POLICY = "default-src 'none'; sandbox";
    public const string CACHE_CONTROL = "private, max-age=31536000, immutable";

    public Task ExecuteResultAsync(ActionContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = CACHE_CONTROL;
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = CONTENT_SECURITY_POLICY;
        return new PhysicalFileResult(file.Path, AssetContentTypes.Served(file.ContentType)) { EntityTag = new EntityTagHeaderValue(file.ETag) }
            .ExecuteResultAsync(context);
    }
}
