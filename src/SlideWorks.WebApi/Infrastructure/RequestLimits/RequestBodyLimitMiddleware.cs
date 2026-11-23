using Microsoft.AspNetCore.Http.Metadata;

namespace SlideWorks.WebApi.Infrastructure.RequestLimits;

// One request-body limit for /mcp and every /api route (K6): a body over the limit is 413 { "error": "body: at most N
// bytes" }, never Kestrel's empty 413, and none of it reaches a tool, a controller or the JSON parser. DEFAULT_BYTES
// applies unless the action says otherwise: [RequestBodyLimit(n)] for a tighter cap, [OwnsBodyLimit] or a framework
// [RequestSizeLimit] / [DisableRequestSizeLimit] when the action enforces a larger cap of its own (uploads, bundles).
//
// A request that declares a Content-Length over the limit is refused without reading a byte. A chunked one has no length to
// check, so its body is read into memory, at most limit + 1 bytes, and replaced by what was read; over the limit it is
// refused. Reading the bytes here, not wrapping the stream, keeps the refusal out of the hands of whatever reads the
// body later (the MCP transport and model binding both turn a failed read into a different error).
public class RequestBodyLimitMiddleware(RequestDelegate next)
{
    // The largest legal add_slides call is bounded by nothing but this: a slide is at most about 7 KB (4000 characters of
    // notes plus its slots), so 1 MB holds more than a hundred full slides, and an ordinary deck of 40 is a tenth of it.
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
        if (request.ContentLength is null && request.Headers.ContainsKey("Transfer-Encoding"))
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
