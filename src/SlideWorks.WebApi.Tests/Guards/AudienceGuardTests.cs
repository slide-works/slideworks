using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Tests.Guards;

// Rules about where code may reach for the hub and for the request's host. They read the production source, because the
// thing they forbid is a line of code, and they fail by naming the file.
[TestFixture]
public class AudienceGuardTests
{
    // A line that reads the request's host or a forwarded one: any receiver and any case of it, the Host header by name or
    // constant, a URL helper that embeds the host, and the forwarded headers.
    private static readonly Regex _hostRead = new(
        @"\.Host\b|Headers\[\s*""Host""\s*\]|HeaderNames\.Host|GetDisplayUrl|GetEncodedUrl|X-Forwarded|XForwarded|ForwardedHost",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Files that build join links or the join object, as globs relative to the WebApi project so new files are covered.
    private static readonly string[] _scanned =
    [
        "Features/Join/**/*.cs",
        "Features/Live/**/*.cs",
        "Features/Decks/**/*.cs",
        "Features/Play/**/*.cs",
        "Features/Render/**/*.cs",
        "Infrastructure/**/*Join*.cs",
        "Realtime/*Audience*.cs",
        "Realtime/*Join*.cs",
    ];

    // The only files allowed to read the Host header (J7): they choose between the two configured origins.
    private static readonly string[] _mayReadHost = ["PublicOriginMiddleware.cs", "JoinHostRouting.cs"];

    // Live updates go through IDeckEvents and IAudienceEvents (J4); only the Realtime folder talks to the hub.
    [Test]
    public void Features_ShouldNeverUseHubContext()
    {
        var files = ProductionSource.Files("Features");

        files.ShouldNotBeEmpty();
        files.Where(file => File.ReadAllText(file).Contains("IHubContext", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    // Join links come from IJoinUrls (J1) and the origin from PublicOriginMiddleware (J7), never from the request's host.
    [Test]
    public void JoinAndLive_ShouldNeverReadRequestHost()
    {
        var files = ProductionSource.Matching(_scanned).Where(f => !_mayReadHost.Contains(Path.GetFileName(f), StringComparer.Ordinal)).ToList();

        files.ShouldNotBeEmpty();
        files.Where(file => _hostRead.IsMatch(File.ReadAllText(file))).ShouldBeEmpty();
    }
}
