using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// Which redirect URIs a client may register (D4; OAuth 2.1 for public clients, RFC 8252 7.1 and 7.3). Never: a relative
// URI, a fragment, whitespace or control characters, userinfo, more than 2000 characters, or a script, file or other
// non-redirect scheme. Otherwise: https with a host; http on localhost, 127.0.0.1 or [::1] (any port); or an app's
// private-use scheme that is reverse-DNS (has a '.') or on ALLOWED_APP_SCHEMES. PKCE S256 is what makes a private-use
// scheme safe: another app that claims it gets a code it can't redeem without the verifier.
public static partial class RedirectUriRules
{
    public const int MAX_REDIRECTS = 5;

    public const int MAX_LENGTH = 2000;

    // Private-use schemes without a '.' that a known agent registers: Cursor's IDE uses cursor://anysphere.cursor-mcp/...
    public static readonly IReadOnlyList<string> ALLOWED_APP_SCHEMES = ["cursor"];

    private static readonly HashSet<string> LOOPBACK_HOSTS = new(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1", "[::1]" };

    private static readonly HashSet<string> REFUSED_SCHEMES = new(StringComparer.OrdinalIgnoreCase)
    {
        "javascript", "data", "file", "vbscript", "blob", "about", "filesystem", "ws", "wss", "ftp", "mailto", "tel",
    };

    public static bool IsAcceptable(string? redirect)
    {
        if (redirect is null || redirect.Length > MAX_LENGTH || redirect.Contains('#', StringComparison.Ordinal)
            || redirect.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))
            || !Uri.TryCreate(redirect, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0)
        {
            return false;
        }
        // The scheme as written. On Linux, Uri reads "/relative/callback" as a file URI, and it lower-cases a scheme, so
        // the written one must be there and agree with it.
        var colon = redirect.IndexOf(':', StringComparison.Ordinal);
        var scheme = colon > 0 ? redirect[..colon] : "";
        if (!string.Equals(scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase) || REFUSED_SCHEMES.Contains(uri.Scheme))
        {
            return false;
        }
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri.Host.Length > 0;
        }
        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            return LOOPBACK_HOSTS.Contains(uri.Host);
        }
        return PrivateUseScheme().IsMatch(scheme) && (scheme.Contains('.', StringComparison.Ordinal) || ALLOWED_APP_SCHEMES.Contains(scheme));
    }

    // True when an authorize redirect_uri is one the client registered: exactly, or for http on a loopback host, the
    // same scheme, host, path and query as written, on any port (RFC 8252 7.3).
    public static bool Matches(IEnumerable<string> registered, string requested)
        => registered.Contains(requested, StringComparer.Ordinal)
            || (WithoutLoopbackPort(requested) is { } asked && registered.Any(r => WithoutLoopbackPort(r) == asked));

    // An http loopback URI with its port taken out, compared as written (no normalising of the path), or null for any
    // other URI.
    private static string? WithoutLoopbackPort(string redirect)
    {
        const string PREFIX = "http://";
        if (!redirect.StartsWith(PREFIX, StringComparison.Ordinal))
        {
            return null;
        }
        var end = redirect.IndexOfAny(['/', '?', '#'], PREFIX.Length);
        var authority = redirect[PREFIX.Length..(end < 0 ? redirect.Length : end)];
        var hostEnd = authority.StartsWith('[') ? authority.IndexOf(']', StringComparison.Ordinal) + 1 : authority.IndexOf(':', StringComparison.Ordinal);
        var host = hostEnd <= 0 ? authority : authority[..hostEnd];
        var port = hostEnd <= 0 ? "" : authority[hostEnd..];
        if (!LOOPBACK_HOSTS.Contains(host) || (port.Length > 0 && (port[0] != ':' || !port[1..].All(char.IsAsciiDigit))))
        {
            return null;
        }
        return PREFIX + host + (end < 0 ? "" : redirect[end..]);
    }

    [GeneratedRegex("^[a-z][a-z0-9+.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateUseScheme();
}
