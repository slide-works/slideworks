using SlideWorks.WebApi.Features.DeviceSessions;

namespace SlideWorks.WebApi.Infrastructure.Identity.Device;

// The device bearer scheme (K5): a phone's swd_ secret in an Authorization bearer header on /api and /hubs, or on /hubs
// only as the access_token query a WebSocket sends. SELECTOR_SCHEME is the app's default scheme: it forwards a request
// that carries a device secret there to SCHEME, and every other request to the cookie scheme. Nothing outside /api and
// /hubs ever reads a device secret, so /mcp, /oauth/*, /.well-known/* and the SPA's routes never accept one.
public static class DeviceBearer
{
    public const string SCHEME = "DeviceBearer";

    public const string SELECTOR_SCHEME = "SlideWorks";

    // The 401 body's error when the secret names an ended or expired session, or a person who is missing or rejected.
    public const string SESSION_ENDED = "session_ended";

    public const string ACCESS_TOKEN_QUERY = "access_token";

    private const string BEARER_PREFIX = "Bearer ";

    private static readonly PathString API_PREFIX = new("/api");

    private static readonly PathString HUBS_PREFIX = new("/hubs");

    // The device secret the request carries, or null when it carries none where one is accepted. An Authorization header
    // with any other bearer (an MCP token) is not a device secret, and neither is an access_token query on /api.
    public static string? SecretOf(HttpRequest request)
    {
        var onHubs = request.Path.StartsWithSegments(HUBS_PREFIX);
        if (!onHubs && !request.Path.StartsWithSegments(API_PREFIX))
        {
            return null;
        }
        var header = request.Headers.Authorization.ToString();
        if (header.StartsWith(BEARER_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            return AsSecret(header[BEARER_PREFIX.Length..].Trim());
        }
        return onHubs ? AsSecret(request.Query[ACCESS_TOKEN_QUERY].ToString()) : null;
    }

    public static bool IsDeviceSecret(string token) => token.StartsWith(DeviceSecrets.SECRET_PREFIX, StringComparison.Ordinal);

    private static string? AsSecret(string token) => IsDeviceSecret(token) ? token : null;
}
