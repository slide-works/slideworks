using System.Globalization;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// How the card engine reaches the SPA and how long a render may take (L9). Production reads both from configuration:
// SLIDEWORKS_RENDER_URL is the origin the headless browser opens (unset means the app's own loopback address), and
// SLIDEWORKS_RENDER_TIMEOUT is a TimeSpan such as 00:00:20 for the whole render, from launching the browser to the PNG.
public sealed record RenderOptions(Uri? BaseUrl, TimeSpan Timeout)
{
    public const string URL_SETTING = "SLIDEWORKS_RENDER_URL";
    public const string TIMEOUT_SETTING = "SLIDEWORKS_RENDER_TIMEOUT";

    public static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromSeconds(20);

    public static RenderOptions From(IConfiguration configuration)
    {
        var url = configuration[URL_SETTING];
        var timeout = configuration[TIMEOUT_SETTING];
        return new RenderOptions(
            string.IsNullOrWhiteSpace(url) ? null : new Uri(url, UriKind.Absolute),
            string.IsNullOrWhiteSpace(timeout) ? DEFAULT_TIMEOUT : TimeSpan.Parse(timeout, CultureInfo.InvariantCulture));
    }
}
