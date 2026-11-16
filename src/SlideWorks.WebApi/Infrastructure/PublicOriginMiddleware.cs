using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Infrastructure;

// When SLIDEWORKS_PUBLIC_URL is set, it is the app's own scheme and host for every request after this point. The image
// serves plain HTTP behind a TLS proxy, so without this a request would look like http://<internal host>: cookies would
// go out without Secure, and sign-in callback URLs would name the wrong address. Taking them from configuration, never
// from the Host or X-Forwarded-* headers a client sends, means a forged header can't change either. Only the scheme
// and host are taken; a path in the public URL is not a PathBase here. With no public URL (Development only), the
// request keeps its own scheme and host.
public class PublicOriginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _scheme;
    private readonly HostString _host;

    public PublicOriginMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var configured = configuration[PublicUrls.CONFIG_KEY];
        // PublicUrls has already refused a value that isn't an absolute http or https URL, at startup.
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var uri = new Uri(configured.Trim(), UriKind.Absolute);
            _scheme = uri.Scheme;
            // Authority leaves out a default port, as a browser's Origin does (HostString.FromUriComponent keeps it).
            _host = new HostString(uri.Authority);
        }
    }

    // AG0003 is about helpers taking the whole HttpContext; a middleware's InvokeAsync has to, it is the shape ASP.NET
    // calls.
#pragma warning disable AG0003
    public Task InvokeAsync(HttpContext context)
#pragma warning restore AG0003
    {
        if (_scheme != null)
        {
            context.Request.Scheme = _scheme;
            context.Request.Host = _host;
        }
        return _next(context);
    }
}
