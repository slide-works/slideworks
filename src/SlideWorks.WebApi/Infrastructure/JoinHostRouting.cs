using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Infrastructure;

// What the join host (SLIDEWORKS_JOIN_URL, such as slideworks.live) serves (J7). PublicOriginMiddleware marks a request whose
// Host is the join host; on those requests "/" is a 302 to /join (D11), the join paths are served, and every other path is a
// 302 to the same path and query on the public URL. A request on any other host is untouched, so the app host serves /join and
// /j/{code} through the SPA fallback like any route. The public URL comes from configuration, never from a header.
public class JoinHostRouting
{
    public const string ON_JOIN_HOST = "SlideWorks.OnJoinHost";

    private static readonly PathString JOIN = new("/join");
    private static readonly PathString[] SERVED_PREFIXES =
    [
        new("/j"), new("/api/join"), new("/api/images"), new("/hubs/deck"), new("/assets"),
    ];
    // Under /.well-known only the two app-link files are served here; the OAuth metadata stays on the public host.
    private static readonly PathString[] SERVED_EXACT =
    [
        JOIN, new("/favicon.svg"), new("/health"),
        new("/.well-known/apple-app-site-association"), new("/.well-known/assetlinks.json"),
    ];

    private readonly RequestDelegate _next;
    private readonly string? _publicBase;

    public JoinHostRouting(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var configured = configuration[PublicUrls.CONFIG_KEY];
        _publicBase = string.IsNullOrWhiteSpace(configured) ? null : new Uri(configured.Trim(), UriKind.Absolute).GetLeftPart(UriPartial.Authority);
    }

    // AG0003 is about helpers taking the whole HttpContext; a middleware's InvokeAsync has to, it is the shape ASP.NET
    // calls.
#pragma warning disable AG0003
    public Task InvokeAsync(HttpContext context)
#pragma warning restore AG0003
    {
        if (!context.Items.ContainsKey(ON_JOIN_HOST))
        {
            return _next(context);
        }
        var request = context.Request;
        if (!request.Path.HasValue || request.Path == "/")
        {
            context.Response.Redirect(JOIN.Value!, permanent: false);
            return Task.CompletedTask;
        }
        if (IsServed(request.Path) || _publicBase == null)
        {
            return _next(context);
        }
        // AG0011 is about controllers binding the query; a redirect has to carry it on as it came.
#pragma warning disable AG0011
        context.Response.Redirect($"{_publicBase}{request.Path}{request.QueryString}", permanent: false);
#pragma warning restore AG0011
        return Task.CompletedTask;
    }

    private static bool IsServed(PathString path)
        => SERVED_EXACT.Any(exact => path.Equals(exact, StringComparison.OrdinalIgnoreCase))
            || SERVED_PREFIXES.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            || IsSixDigits(path.Value!);

    private static bool IsSixDigits(string path) => path.Length == 7 && path[0] == '/' && path.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;
}
