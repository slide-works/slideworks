using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Infrastructure;

// When SLIDEWORKS_PUBLIC_URL is set, it is the app's own scheme and host for every request after this point. The image
// serves plain HTTP behind a TLS proxy, so without this a request would look like http://<internal host>: cookies would
// go out without Secure, and sign-in callback URLs would name the wrong address. Taking them from configuration, never
// from the Host or X-Forwarded-* headers a client sends, means a forged header can't change either. Only the scheme
// and host are taken; a path in the public URL is not a PathBase here. With no public URL (Development only), the
// request keeps its own scheme and host.
//
// When SLIDEWORKS_JOIN_URL is set too (J7), a request whose Host equals the join host gets the join origin instead, and
// is marked for JoinHostRouting. Those two configured origins are the only ones ever chosen: any other Host, and any
// X-Forwarded-Host, gets the public origin. So the proxy must pass the Host header through unchanged.
public class PublicOriginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Origin? _public;
    private readonly Origin? _join;

    public PublicOriginMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        // PublicUrls and JoinUrls have already refused a value that isn't a valid http or https URL, at startup.
        _public = Origin.From(configuration[PublicUrls.CONFIG_KEY]);
        _join = Origin.From(configuration[JoinUrls.CONFIG_KEY]);
    }

    // AG0003 is about helpers taking the whole HttpContext; a middleware's InvokeAsync has to, it is the shape ASP.NET
    // calls.
#pragma warning disable AG0003
    public Task InvokeAsync(HttpContext context)
#pragma warning restore AG0003
    {
        var onJoinHost = _join != null && _join.Host.Equals(context.Request.Host);
        var chosen = onJoinHost ? _join : _public;
        if (chosen != null)
        {
            context.Request.Scheme = chosen.Scheme;
            context.Request.Host = chosen.Host;
        }
        if (onJoinHost)
        {
            context.Items[JoinHostRouting.ON_JOIN_HOST] = true;
        }
        return _next(context);
    }

    // Authority leaves out a default port, as a browser's Origin does (HostString.FromUriComponent keeps it).
    private sealed record Origin(string Scheme, HostString Host)
    {
        public static Origin? From(string? configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                return null;
            }
            var uri = new Uri(configured.Trim(), UriKind.Absolute);
            return new Origin(uri.Scheme, new HostString(uri.Authority));
        }
    }
}
