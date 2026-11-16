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
            return uri.Authority;
        }
        return uri.Host.Length > 0 ? $"{uri.Scheme}://{uri.Host}" : $"{uri.Scheme}:";
    }
}
