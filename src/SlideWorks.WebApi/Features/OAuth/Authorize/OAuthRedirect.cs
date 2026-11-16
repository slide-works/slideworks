using Microsoft.AspNetCore.WebUtilities;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The client's registered redirect URI with the answer added to its query, keeping any query it already has. A null
// value (such as a missing state) is left out.
public static class OAuthRedirect
{
    public static string To(string redirectUri, params (string Name, string? Value)[] parameters)
        => QueryHelpers.AddQueryString(
            redirectUri,
            parameters.Where(p => p.Value != null).Select(p => new KeyValuePair<string, string?>(p.Name, p.Value)));
}
