using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi;

namespace SlideWorks.AcceptanceTests;

// Starts the real BFF once for the whole run, on a real Kestrel port a browser can reach:
// the production Program.cs, the production DI and the SPA bundle that
// `pnpm run build` emitted into SlideWorks.WebApi/wwwroot. Only the database file and the
// token are the test's own. Browsers and the SPA never see a fake.
[SetUpFixture]
public class AcceptanceApp
{
    public const string TOKEN = "acceptance-test-token";

    private string _dbPath = null!;
    private WebApplicationFactory<ApiMarker> _factory = null!;

    public static string BaseUrl { get; private set; } = null!;

    [OneTimeSetUp]
    public void StartApp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"slide-works-acceptance-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<ApiMarker>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SLIDEWORKS_DB_PATH"] = _dbPath,
                    ["SLIDEWORKS_API_TOKEN"] = TOKEN,
                })));
        _factory.UseKestrel(0);
        _factory.StartServer();

        AssertBundleIsBuilt();
        BaseUrl = ListeningUrl();
    }

    [OneTimeTearDown]
    public void StopApp()
    {
        _factory.Dispose();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            File.Delete(_dbPath + suffix);
        }
    }

    // The tests drive the shipped bundle, not a dev server, so it has to exist first.
    private void AssertBundleIsBuilt()
    {
        var webRoot = _factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        File.Exists(Path.Combine(webRoot ?? string.Empty, "index.html")).ShouldBeTrue(
            "The SPA bundle is missing. Build the client first: cd src/SlideWorks.Client && pnpm install && pnpm run build "
            + "(it emits into src/SlideWorks.WebApi/wwwroot).");
    }

    // Port 0 means Kestrel picks one; the address it bound is only known once it is listening.
    private string ListeningUrl()
    {
        var address = _factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return $"http://127.0.0.1:{new Uri(address.Replace("[::]", "localhost")).Port}";
    }
}
