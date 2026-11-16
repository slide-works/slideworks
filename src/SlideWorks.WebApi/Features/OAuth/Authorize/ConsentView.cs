using SlideWorks.WebApi.Features.OAuth.Stores;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The consent screen: which agent asks, for what, and where the answer goes. ClientName is whatever the client chose
// when it registered, so ReturnsTo names the destination the server will actually send the browser to: the host (and
// port) of a web redirect, such as localhost:33418, or an app's own scheme, such as cursor://anysphere.cursor-mcp.
public sealed record ConsentView(string ClientName, string Scope, string ReturnsTo)
{
    public static ConsentView From(OAuthClient client, PendingAuthorization pending)
        => new(client.ClientName, pending.Scope, ReturnsToOf(pending.RedirectUri));

    private static string ReturnsToOf(string redirectUri)
    {
        var uri = new Uri(redirectUri, UriKind.Absolute);
        if (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        {
            // The host as the browser will look it up: punycode, never Unicode that could pass for another host.
            var host = uri.HostNameType == UriHostNameType.IPv6 ? uri.Host : uri.IdnHost;
            return uri.IsDefaultPort ? host : $"{host}:{uri.Port}";
        }
        return uri.Host.Length > 0 ? $"{uri.Scheme}://{uri.Host}" : $"{uri.Scheme}:";
    }
}
