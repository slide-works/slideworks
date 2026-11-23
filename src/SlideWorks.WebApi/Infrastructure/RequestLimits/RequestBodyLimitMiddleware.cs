using Microsoft.AspNetCore.Http.Metadata;

namespace SlideWorks.WebApi.Infrastructure.RequestLimits;

// One request-body limit for /mcp and every /api route (K6): a body over the limit is 413 { "error": "body: at most N
// bytes" }, never Kestrel's empty 413, and none of it reaches a tool, a controller or the JSON parser. DEFAULT_BYTES
// applies unless the action says otherwise: [RequestBodyLimit(n)] for a tighter cap, [OwnsBodyLimit] or a framework
// [RequestSizeLimit] / [DisableRequestSizeLimit] when the action enforces a larger cap of its own (uploads, bundles).
//
// A request that declares a Content-Length over the limit is refused without reading a byte. One whose length is unknown
// (chunked, or an HTTP/2 or HTTP/3 body with no Content-Length) has no length to check, so its body is read into memory, at most limit + 1 bytes, and replaced by what was read; over the limit it is
// refused. Reading the bytes here, not wrapping the stream, keeps the refusal out of the hands of whatever reads the
// body later (the MCP transport and model binding both turn a failed read into a different error).
public class RequestBodyLimitMiddleware(RequestDelegate next)
{
    // Nothing in the product bounds an add_slides batch: there is no per-call slide count, no deck slide cap and no per-slot
    // size limit (only notes, at 4000 characters, are capped), so no "largest legal call" exists to fit. 1 MB is a chosen
    // limit, not a derived one: a typical slide is 1 to 2 KB of JSON, so it holds several hundred slides, and an ordinary
    // 40-slide deck is a few percent of it. A larger batch is refused with 413 and the agent splits it.
    public const int DEFAULT_BYTES = 1024 * 1024;

    public static string TooLarge(int maxBytes) => $"body: at most {maxBytes} bytes";

    // AG0003 is about helpers taking the whole HttpContext; a middleware's InvokeAsync has to, it is the shape ASP.NET
    // calls.
#pragma warning disable AG0003
    public async Task InvokeAsync(HttpContext context)
#pragma warning restore AG0003
    {
        var limit = LimitFor(context.Request);
        if (limit is null)
        {
            await next(context);
            return;
        }
        var request = context.Request;
        if (request.ContentLength > limit)
        {
            await RefuseAsync(context.Response, limit.Value, context.RequestAborted);
            return;
        }
        // No Content-Length means the length is unknown, whatever Transfer-Encoding says: HTTP/2 and HTTP/3 send a body with
        // neither header. Every method is bounded, GET and HEAD included: HTTP lets them carry a body, and reading an empty
        // one costs nothing.
        if (request.ContentLength is null)
        {
            var body = await ReadAtMostAsync(request.Body, limit.Value, context.RequestAborted);
            if (body is null)
            {
                await RefuseAsync(context.Response, limit.Value, context.RequestAborted);
                return;
            }
            request.Body = body;
        }
        await next(context);
    }

    private static int? LimitFor(HttpRequest request)
    {
        var path = request.Path;
        if (!path.StartsWithSegments("/mcp") && !path.StartsWithSegments("/api"))
        {
            return null;
        }
        var metadata = request.HttpContext.GetEndpoint()?.Metadata;
        if (metadata?.GetMetadata<OwnsBodyLimitAttribute>() != null
            || metadata?.GetMetadata<IRequestSizeLimitMetadata>() != null)
        {
            return null;
        }
        return metadata?.GetMetadata<RequestBodyLimitAttribute>()?.MaxBytes ?? DEFAULT_BYTES;
    }

    // The body as a MemoryStream, or null when it is longer than limit.
    private static async Task<MemoryStream?> ReadAtMostAsync(Stream body, int limit, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        buffer.Position = 0;
        return buffer;
    }

    private static async Task RefuseAsync(HttpResponse response, int limit, CancellationToken ct)
    {
        response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        // The rest of the body is never read; telling the client the connection ends with this response lets the server
        // drop it instead of draining what may be gigabytes.
        response.Headers.Connection = "close";
        await response.WriteAsJsonAsync(new ErrorResponse(TooLarge(limit)), ct);
    }
}
