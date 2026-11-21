using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// SLIDEWORKS_JOIN_URL, read once. It is optional. When it is set it must be an absolute http or https URL with no path,
// query or fragment, and the constructor throws otherwise. Both JoinUrls and PublicUrls take it, and Program.cs resolves
// PublicUrls at startup, so a bad value stops the server from starting. It exists so PublicUrls can run that check without
// taking IJoinUrls, which takes IPublicUrls.
[RegisterSingleton]
public class JoinUrlSetting
{
    public const string CONFIG_KEY = "SLIDEWORKS_JOIN_URL";

    public JoinUrlSetting(IConfiguration configuration)
    {
        Value = Parse(configuration[CONFIG_KEY]);
    }

    // The configured URL, or null when none is set.
    public Uri? Value { get; }

    private static Uri? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var text = value.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{CONFIG_KEY} must be an absolute http or https URL, not '{value}'.");
        }
        if (uri.UserInfo.Length > 0)
        {
            throw new InvalidOperationException($"{CONFIG_KEY} must not contain a user name or password.");
        }
        if (uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            throw new InvalidOperationException($"{CONFIG_KEY} must be an origin with no path, query or fragment, such as https://slideworks.live, not '{value}'.");
        }
        return uri;
    }
}
