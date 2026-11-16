using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Render;

// A share card as image/png, with its RenderKey as the ETag (L9). Link previewers revalidate (no-cache), and a matching
// If-None-Match is 304 with no body, so a card is sent again only when its revision, look or sharing changed. A card
// that stands in for a failed render (cacheable false, SW-81 D5) is no-store with no ETag, so the deck's own card
// replaces it as soon as it renders.
public sealed class CardPngResult(RenderedCard card, bool cacheable = true) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;
        if (!cacheable)
        {
            response.Headers.CacheControl = "no-store";
            await WriteAsync(context);
            return;
        }
        var etag = new EntityTagHeaderValue($"\"{card.Key}\"");
        response.Headers.ETag = etag.ToString();
        response.Headers.CacheControl = "public, no-cache";
        if (request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(etag, useStrongComparison: false)))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }
        await WriteAsync(context);
    }

    private async Task WriteAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "image/png";
        response.ContentLength = card.Png.Length;
        await response.Body.WriteAsync(card.Png, context.HttpContext.RequestAborted);
    }
}
