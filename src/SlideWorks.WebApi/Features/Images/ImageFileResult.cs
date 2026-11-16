using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace SlideWorks.WebApi.Features.Images;

// Sends an image file (K11) with its stored type and ETag, Cache-Control: private, no-cache (so a browser asks again every
// time and revoking access takes effect at once; If-None-Match gets a 304) and nosniff, so the browser never reads it as
// anything but its stored type.
public sealed class ImageFileResult(StoredImage image) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "private, no-cache";
        headers.XContentTypeOptions = "nosniff";
        return new PhysicalFileResult(image.Path, image.ContentType) { EntityTag = new EntityTagHeaderValue(image.ETag) }
            .ExecuteResultAsync(context);
    }
}
