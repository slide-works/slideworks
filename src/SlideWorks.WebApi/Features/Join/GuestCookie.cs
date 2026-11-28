using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace SlideWorks.WebApi.Features.Join;

// The sw_guest cookie (J3, D12): a guest's identity for one phone. 128 random bits as 32 lowercase hex digits, HttpOnly,
// SameSite=Lax, Secure on https, Path=/, Max-Age 12 hours. It is scoped to the host that set it, and it is not a login:
// it only says "this is a guest", so a value that isn't one of ours is treated as no cookie. The app sends no cookies, so it
// carries the same id in the X-SlideWorks-Guest header, and on the hub (a WebSocket can't send a header) in the guest query
// (K7, D7). This is the one place that reads a guest id, whichever way it came.
public static partial class GuestCookie
{
    public const string NAME = "sw_guest";

    // The header the app sends its guest id in, and the one a join answers with when it mints the app an id (D10).
    public const string HEADER = "X-SlideWorks-Guest";

    // The header that says the caller is the app, so a join hands it the id in HEADER instead of a cookie (D10).
    public const string CLIENT_HEADER = "X-SlideWorks-Client";

    public const string APP_CLIENT = "app";

    // The query a hub connection carries the guest id in. Read on /hubs only; anywhere else it is ignored.
    public const string QUERY = "guest";

    private static readonly PathString HUBS = new("/hubs");

    public static readonly TimeSpan LIFETIME = TimeSpan.FromHours(12);

    private const int ID_BYTES = 16;

    // The guest id the request carries, or null when it has none or it isn't one we minted: the cookie's when valid, else the
    // header's, else (on /hubs only) the query's. A malformed value in one place counts as none there.
    public static string? From(HttpRequest request)
        => Valid(request.Cookies.TryGetValue(NAME, out var cookie) ? cookie : null)
            ?? Valid(Single(request.Headers[HEADER]))
            ?? (request.Path.StartsWithSegments(HUBS) ? Valid(Single(request.Query[QUERY])) : null);

    // The same rule for a caller that holds the HttpContext, such as the hub: the context may be null on a connection with none.
    public static string? IdOf(HttpContext? context) => context is null ? null : From(context.Request);

    // Mints a guest id, the one format From accepts. Tests that act as a guest use this too.
    public static string NewId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(ID_BYTES));

    // Gives the caller a new guest id unless the request already carries a valid one: the app (CLIENT_HEADER: app) gets it in
    // the HEADER response header and no cookie, everyone else the cookie.
    public static void EnsureOn(HttpRequest request, HttpResponse response)
    {
        if (From(request) is not null)
        {
            return;
        }
        if (Single(request.Headers[CLIENT_HEADER]) == APP_CLIENT)
        {
            response.Headers[HEADER] = NewId();
            return;
        }
        // S2092 wants Secure set unconditionally. It is set whenever the request is https, which PublicOriginMiddleware takes
        // from the public URL; a plain-http local run still gets the cookie.
#pragma warning disable S2092
        response.Cookies.Append(NAME, NewId(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = request.IsHttps,
            Path = "/",
            MaxAge = LIFETIME,
            IsEssential = true,
        });
#pragma warning restore S2092
    }

    private static string? Valid(string? value) => value is not null && IdPattern().IsMatch(value) ? value : null;

    // A value sent more than once is not one guest id.
    private static string? Single(StringValues values) => values.Count == 1 ? values[0] : null;

    [GeneratedRegex(@"^[0-9a-f]{32}\z")]
    private static partial Regex IdPattern();
}
