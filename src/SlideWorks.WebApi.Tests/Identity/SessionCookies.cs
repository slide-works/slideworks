using System.Net.Http;

namespace SlideWorks.WebApi.Tests.Identity;

// Reads the sw-session Set-Cookie header's attributes, lower-cased, such as "secure" or "samesite=lax".
public static class SessionCookies
{
    public static IReadOnlyList<string> Attributes(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("sw-session=", StringComparison.Ordinal));
        return header.Split(';').Skip(1).Select(part => part.Trim().ToLowerInvariant()).ToArray();
    }
}
