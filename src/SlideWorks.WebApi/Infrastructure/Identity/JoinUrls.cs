using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// SLIDEWORKS_JOIN_URL is optional. When it is set it must be an absolute http or https URL with no path, query or fragment,
// and the constructor throws otherwise. PublicUrls takes this at construction and Program.cs resolves PublicUrls at startup,
// so a bad value stops the server from starting.
//
// IPublicUrls is read through the service provider at call time because PublicUrls itself takes IJoinUrls for that check.
[RegisterSingleton(For = typeof(IJoinUrls))]
public class JoinUrls : IJoinUrls
{
    public const string CONFIG_KEY = "SLIDEWORKS_JOIN_URL";

    private const string PUBLIC_PATH = "/j";

    private readonly Uri? _configured;
    private readonly IServiceProvider _services;

    public JoinUrls(IConfiguration configuration, IServiceProvider services)
    {
        _services = services;
        _configured = Parse(configuration[CONFIG_KEY]);
    }

    public string JoinUrl(string code) => _configured == null ? $"{PublicBase()}{PUBLIC_PATH}/{code}" : $"{Origin(_configured)}/{code}";

    public string KeypadUrl() => $"{(_configured == null ? PublicBase() : Origin(_configured))}/join";

    // The authority keeps a non-default port, so a join URL on a port prints with it.
    public string HostLabel()
        => _configured == null ? $"{new Uri(PublicBase()).Authority}{PUBLIC_PATH}" : _configured.Authority;

    private string PublicBase() => _services.GetRequiredService<IPublicUrls>().Base;

    private static string Origin(Uri uri) => uri.GetLeftPart(UriPartial.Authority);

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
        if (uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            throw new InvalidOperationException($"{CONFIG_KEY} must be an origin with no path, query or fragment, such as https://slideworks.live, not '{value}'.");
        }
        return uri;
    }
}
