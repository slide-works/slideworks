namespace SlideWorks.WebApi.Tests.Guards;

// Rules about where code may reach for the hub and for the request's host. They read the production source, because the
// thing they forbid is a line of code, and they fail by naming the file.
[TestFixture]
public class AudienceGuardTests
{
    private static readonly string[] _hostReads =
    [
        "Request.Host",
        "Request.Headers.Host",
        "Headers.Host",
        "HeaderNames.Host",
        "X-Forwarded",
        "XForwarded",
        "ForwardedHost",
    ];

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
        var files = ProductionSource.Files("Features/Join")
            .Concat(ProductionSource.Files("Features/Live"))
            .Concat(ProductionSource.Files("Realtime").Where(f => Path.GetFileName(f).StartsWith("Audience", StringComparison.Ordinal)
                || Path.GetFileName(f).StartsWith("IAudience", StringComparison.Ordinal)
                || Path.GetFileName(f).StartsWith("StubAudience", StringComparison.Ordinal)
                || Path.GetFileName(f).Contains("Join", StringComparison.Ordinal)))
            .ToList();

        files.ShouldNotBeEmpty();
        files.Where(file => _hostReads.Any(read => File.ReadAllText(file).Contains(read, StringComparison.Ordinal))).ShouldBeEmpty();
    }
}
