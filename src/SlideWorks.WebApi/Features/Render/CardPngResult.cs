using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Render;

// A share card as image/png, with its RenderKey as the ETag (L9). Link previewers revalidate (no-cache), and a matching
// If-None-Match is 304 with no body, so a card is sent again only when its revision, look or sharing changed.
public sealed class CardPngResult(RenderedCard card) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;
        var etag = new EntityTagHeaderValue($"\"{card.Key}\"");
        response.Headers.ETag = etag.ToString();
        response.Headers.CacheControl = "public, no-cache";
        if (request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(etag, useStrongComparison: false)))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "image/png";
        response.ContentLength = card.Png.Length;
        await response.Body.WriteAsync(card.Png, context.HttpContext.RequestAborted);
    }
}
