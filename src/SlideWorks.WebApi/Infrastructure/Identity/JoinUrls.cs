using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// The join links. SLIDEWORKS_JOIN_URL is parsed and checked by JoinUrlSetting, which PublicUrls also takes, so a bad value
// stops the server from starting. With no join URL the links hang off IPublicUrls.
[RegisterSingleton(For = typeof(IJoinUrls))]
public class JoinUrls : IJoinUrls
{
    public const string CONFIG_KEY = JoinUrlSetting.CONFIG_KEY;

    private const string PUBLIC_PATH = "/j";

    private readonly Uri? _configured;
    private readonly IPublicUrls _publicUrls;

    public JoinUrls(JoinUrlSetting setting, IPublicUrls publicUrls)
    {
        _publicUrls = publicUrls;
        _configured = setting.Value;
    }

    public string JoinUrl(string code) => _configured == null ? $"{PublicBase()}{PUBLIC_PATH}/{code}" : $"{Origin(_configured)}/{code}";

    public string KeypadUrl() => $"{(_configured == null ? PublicBase() : Origin(_configured))}/join";

    // The authority keeps a non-default port, so a join URL on a port prints with it.
    public string HostLabel()
        => _configured == null ? $"{new Uri(PublicBase()).Authority}{PUBLIC_PATH}" : _configured.Authority;

    private string PublicBase() => _publicUrls.Base;

    private static string Origin(Uri uri) => uri.GetLeftPart(UriPartial.Authority);
}
