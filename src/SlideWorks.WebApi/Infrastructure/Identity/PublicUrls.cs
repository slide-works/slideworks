using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// SLIDEWORKS_PUBLIC_URL is required outside Development, and the constructor throws without it, so Program.cs
// resolves this at startup to fail fast. In Development with no value, a request from localhost or 127.0.0.1 supplies
// the base, and any other Host is refused.
[RegisterSingleton(For = typeof(IPublicUrls))]
public class PublicUrls : IPublicUrls
{
    public const string CONFIG_KEY = "SLIDEWORKS_PUBLIC_URL";

    private static readonly HashSet<string> LOCAL_HOSTS = new(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1", "[::1]" };

    private readonly string? _configured;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // AG0009 is about passing the whole HttpContext into helpers. The accessor is the only way a singleton reaches the
    // current request, and it is read only in Development with no configured URL.
#pragma warning disable AG0009
    public PublicUrls(IConfiguration configuration, IHostEnvironment environment, IHttpContextAccessor httpContextAccessor)
#pragma warning restore AG0009
    {
        _httpContextAccessor = httpContextAccessor;
        _configured = Parse(configuration[CONFIG_KEY]);
        if (_configured == null && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{CONFIG_KEY} is required outside Development. Set it to the URL people and agents reach SlideWorks at, such as https://slideworks.example.");
        }
    }

    public string Base => _configured ?? FromLocalRequest();

    public string Mcp => $"{Base}/mcp";

    public string Deck(string id, string slug, int? slide = null, int? step = null)
    {
        var url = $"{Base}/d/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(slug)}";
        if (slide == null)
        {
            return url;
        }
        return step == null ? $"{url}#{slide}" : $"{url}#{slide}.{step}";
    }

    private static string? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{CONFIG_KEY} must be an absolute http or https URL, not '{value}'.");
        }
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private string FromLocalRequest()
    {
        var request = _httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException($"{CONFIG_KEY} is not set, and there is no request to take a local URL from.");
        if (!LOCAL_HOSTS.Contains(request.Host.Host))
        {
            throw new InvalidOperationException(
                $"{CONFIG_KEY} is not set, and the request's host '{request.Host.Host}' is not localhost. Set {CONFIG_KEY}.");
        }
        return $"{request.Scheme}://{request.Host.Value}";
    }
}
