using SlideWorks.WebApi.Features.Render;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// Refuses a write under /api that another site could have made the browser send (K1). The session cookie is SameSite=Lax,
// and this is the second line: every request under /api with a method other than GET, HEAD, OPTIONS or TRACE, however it
// was authenticated, is refused with 403 when its Origin is present and isn't SlideWorks' own, or when it comes from a
// signed-in caller with neither an Origin nor Sec-Fetch-Site: same-origin. Browsers send Origin on every cross-site
// write, so a forged one can't pass. /mcp (bearer tokens, not cookies) and /oauth/* (form posts from agents) are outside
// /api, so outside this guard.
//
// The realtime hub (/hubs/*) is checked too, for every method: a negotiate or a WebSocket connect whose Origin is present
// and isn't SlideWorks' own gets 403, so another site's page can't follow decks with a viewer's cookie. Browsers always
// send Origin on a WebSocket handshake; a caller with no Origin isn't a browser on another site, so it passes.
//
// SlideWorks' own origin is the request's scheme and host. PublicOriginMiddleware runs first and sets them from
// SLIDEWORKS_PUBLIC_URL when it is configured, so behind a proxy (or the Vite dev server) the configured URL is what a
// browser's Origin is compared with, and a forged Host header changes nothing. With no public URL (Development only),
// it is the address the request was made to.
//
// One write passes whatever its Origin: a write under /api/render that carries the X-Render-Token header. The server's
// headless renderer (check_deck) opens the render page at its own address (SLIDEWORKS_RENDER_URL, or Kestrel's loopback),
// which is not the public URL, so the page's POST /api/render/{id}/session carries an Origin the guard would call foreign.
// That request rides on no cookie: the header is its only credential, another site's page can't add a custom header
// without a CORS preflight SlideWorks never grants, and the route accepts nothing but a valid token for its deck.
public class CrossSiteWriteGuard(RequestDelegate next)
{
    private static readonly PathString API_PREFIX = new("/api");
    private static readonly PathString HUBS_PREFIX = new("/hubs");
    private static readonly PathString RENDER_PREFIX = new("/api/render");

    // AG0003 is about helpers taking the whole HttpContext; a middleware's InvokeAsync has to, it is the shape ASP.NET
    // calls.
#pragma warning disable AG0003
    public async Task InvokeAsync(HttpContext context)
#pragma warning restore AG0003
    {
        var request = context.Request;
        var refused = IsGuardedWrite(request)
            ? !IsSameSite(request, context.User.Identity?.IsAuthenticated == true) && !IsRenderTokenWrite(request)
            : request.Path.StartsWithSegments(HUBS_PREFIX) && HasForeignOrigin(request);
        if (refused)
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

    private static bool IsRenderTokenWrite(HttpRequest request)
        => request.Path.StartsWithSegments(RENDER_PREFIX) && request.Headers[RenderSessions.HEADER].ToString().Length > 0;

    private static bool IsSameSite(HttpRequest request, bool signedIn)
    {
        if (request.Headers.Origin.ToString().Length > 0)
        {
            return !HasForeignOrigin(request);
        }
        return !signedIn || string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "same-origin", StringComparison.Ordinal);
    }

    private static bool HasForeignOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        return origin.Length > 0 && !string.Equals(origin, $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase);
    }
}
