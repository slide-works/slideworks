namespace SlideWorks.WebApi.Infrastructure.Identity;

// Refuses a write under /api that another site could have made the browser send (K1). The session cookie is SameSite=Lax,
// and this is the second line: every request under /api with a method other than GET, HEAD, OPTIONS or TRACE, however it
// was authenticated, is refused with 403 when its Origin is present and isn't SlideWorks' own, or when it comes from a
// signed-in caller with neither an Origin nor Sec-Fetch-Site: same-origin. Browsers send Origin on every cross-site
// write, so a forged one can't pass. /mcp (bearer tokens, not cookies) and /oauth/* (form posts from agents) are outside
// /api, so outside this guard.
//
// SlideWorks' own origin is the request's scheme and host. PublicOriginMiddleware runs first and sets them from
// SLIDEWORKS_PUBLIC_URL when it is configured, so behind a proxy (or the Vite dev server) the configured URL is what a
// browser's Origin is compared with, and a forged Host header changes nothing. With no public URL (Development only),
// it is the address the request was made to.
public class CrossSiteWriteGuard(RequestDelegate next)
{
    private static readonly PathString API_PREFIX = new("/api");

    // AG0003 is about helpers taking the whole HttpContext; a middleware's InvokeAsync has to, it is the shape ASP.NET
    // calls.
#pragma warning disable AG0003
    public async Task InvokeAsync(HttpContext context)
#pragma warning restore AG0003
    {
        if (IsGuardedWrite(context.Request) && !IsSameSite(context.Request, context.User.Identity?.IsAuthenticated == true))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("Cross-site request refused"), context.RequestAborted);
            return;
        }
        await next(context);
    }

    private static bool IsGuardedWrite(HttpRequest request)
        => request.Path.StartsWithSegments(API_PREFIX) && !IsSafe(request.Method);

    // Safe by definition (RFC 9110), so no write rides on them. Every other method is guarded, including ones nothing
    // routes today.
    private static bool IsSafe(string method)
        => HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    private static bool IsSameSite(HttpRequest request, bool signedIn)
    {
        var origin = request.Headers.Origin.ToString();
        if (origin.Length > 0)
        {
            return string.Equals(origin, $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase);
        }
        return !signedIn || string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "same-origin", StringComparison.Ordinal);
    }
}
