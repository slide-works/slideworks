using System.Net;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// Which redirect URIs a client may register (OAuth 2.1 for public clients, RFC 8252 7.3): https, or http on a loopback
// host (any port). No userinfo, no fragment.
public static class RedirectUriRules
{
    public const int MAX_REDIRECTS = 5;

    public static bool IsAcceptable(string? redirect)
    {
        if (redirect is null || redirect.Contains('#', StringComparison.Ordinal) || redirect.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))
            || !Uri.TryCreate(redirect, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Host.Length == 0)
        {
            return false;
        }
        return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri));
    }

    private static bool IsLoopback(Uri uri)
        => string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address));
}
