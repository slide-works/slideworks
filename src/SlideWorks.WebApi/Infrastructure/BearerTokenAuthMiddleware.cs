using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.WebApi.Infrastructure;

// A static bearer token (env SLIDEWORKS_API_TOKEN) protects /api and the realtime
// hubs under /hubs. Fail closed: with no token configured, everything except
// /health is rejected.
//
// SLIDEWORKS_API_EXTRA_TOKENS is a comma-separated list of further tokens accepted
// alongside it, so another app gets its own token rather than a copy of yours,
// and either can be rotated alone.
public class BearerTokenAuthMiddleware
{
    private const string BEARER_PREFIX = "Bearer ";
    private static readonly PathString HUBS_PREFIX = new("/hubs");
    private static readonly PathString[] PROTECTED_PREFIXES = [new("/api"), HUBS_PREFIX];
    private static readonly PathString HEALTH_PATH = new("/health");

    private readonly RequestDelegate _next;
    private readonly List<byte[]> _tokens;

    public BearerTokenAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _tokens = (configuration["SLIDEWORKS_API_EXTRA_TOKENS"] ?? "")
            .Split(',', StringSplitOptions.TrimEntries)
            .Prepend(configuration["SLIDEWORKS_API_TOKEN"])
            .Where(token => !string.IsNullOrEmpty(token))
            .Select(token => Encoding.UTF8.GetBytes(token!))
            .ToList();
    }

    // AG0003 is about helpers taking the whole HttpContext; a middleware's
    // InvokeAsync has to, it is the shape ASP.NET calls.
#pragma warning disable AG0003
    public async Task InvokeAsync(HttpContext context)
#pragma warning restore AG0003
    {
        var path = context.Request.Path;

        if (_tokens.Count == 0)
        {
            if (path.StartsWithSegments(HEALTH_PATH))
            {
                await _next(context);
                return;
            }
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (PROTECTED_PREFIXES.Any(prefix => path.StartsWithSegments(prefix)) && !IsAuthorized(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    private bool IsAuthorized(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (header.StartsWith(BEARER_PREFIX, StringComparison.Ordinal))
        {
            return Matches(header[BEARER_PREFIX.Length..].Trim());
        }
        // A browser can't set headers on a WebSocket, so the SignalR client sends
        // the token as ?access_token= on the upgrade request. Accepted on /hubs only.
        // Request URLs are not logged (Microsoft.AspNetCore logs at Warning).
        var query = request.Query["access_token"].ToString();
        return request.Path.StartsWithSegments(HUBS_PREFIX) && query.Length > 0 && Matches(query);
    }

    private bool Matches(string token)
    {
        var presented = Encoding.UTF8.GetBytes(token);
        return _tokens.Any(candidate => CryptographicOperations.FixedTimeEquals(presented, candidate));
    }
}
