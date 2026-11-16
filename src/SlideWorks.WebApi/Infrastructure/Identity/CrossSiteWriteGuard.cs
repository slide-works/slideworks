namespace SlideWorks.WebApi.Infrastructure.Identity;

// Refuses a write under /api that another site could have made the browser send (K1). The session cookie is SameSite=Lax,
// and this is the second line: every POST, PUT, PATCH and DELETE under /api, however it was authenticated, is refused
// with 403 when its Origin is present and isn't SlideWorks' own, or when it comes from a signed-in caller with neither an
// Origin nor Sec-Fetch-Site: same-origin. Browsers send Origin on every cross-site write, so a forged one can't pass.
// /mcp (bearer tokens, not cookies) and /oauth/* (form posts from agents) are outside /api, so outside this guard.
public class CrossSiteWriteGuard(RequestDelegate next)
{
    private static readonly PathString API_PREFIX = new("/api");
    private static readonly string[] UNSAFE_METHODS = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    // AG0003 is about helpers taking the whole HttpContext; a middleware's InvokeAsync has to, it is the shape ASP.NET
    // calls.
#pragma warning disable AG0003
    public async Task InvokeAsync(HttpContext context, IPublicUrls publicUrls)
#pragma warning restore AG0003
    {
        if (IsGuardedWrite(context.Request) && !IsSameSite(context.Request, context.User.Identity?.IsAuthenticated == true, publicUrls))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("Cross-site request refused"), context.RequestAborted);
            return;
        }
        await next(context);
    }

    private static bool IsGuardedWrite(HttpRequest request)
        => request.Path.StartsWithSegments(API_PREFIX) && UNSAFE_METHODS.Any(method => HttpMethods.Equals(method, request.Method));

    private static bool IsSameSite(HttpRequest request, bool signedIn, IPublicUrls publicUrls)
    {
        var origin = request.Headers.Origin.ToString();
        if (origin.Length > 0)
        {
            return string.Equals(origin, OriginOf(publicUrls.Base), StringComparison.OrdinalIgnoreCase);
        }
        return !signedIn || string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "same-origin", StringComparison.Ordinal);
    }

    private static string OriginOf(string url) => new Uri(url).GetLeftPart(UriPartial.Authority);
}
