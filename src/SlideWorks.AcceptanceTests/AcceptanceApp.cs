using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// Starts the real BFF once for the whole run, on a real Kestrel port a browser can reach: the production Program.cs,
// the production DI and the SPA bundle that `pnpm run build` emitted into SlideWorks.WebApi/wwwroot. Only the database
// file and the data folder (where uploaded images go) are the run's own, both deleted when the run ends. The test
// sign-in scheme is added so SignIn.AsAsync can sign a browser in as a TestUser, and SLIDEWORKS_ADMIN_EMAIL is Ada's, so
// she is the admin from her first sign-in.
// Browsers and the SPA never see a fake.
//
// The port is reserved before the host starts, so SLIDEWORKS_PUBLIC_URL can name it: links, OAuth metadata, the MCP
// resource and the cross-site write guard then all agree with the address the browser uses, even outside a request.
[SetUpFixture]
public class AcceptanceApp
{
    private string _dbPath = null!;
    private string _dataPath = null!;
    private WebApplicationFactory<ApiMarker> _factory = null!;

    public static string BaseUrl { get; private set; } = null!;

    // The running app's container, for the seams an OAuth flow ends in (see AgentSeed). Never for the database.
    public static IServiceProvider Services { get; private set; } = null!;

    [OneTimeSetUp]
    public void StartApp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"slide-works-acceptance-{Guid.NewGuid():N}.db");
        _dataPath = Directory.CreateTempSubdirectory("slide-works-acceptance-data-").FullName;
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _factory = new WebApplicationFactory<ApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SLIDEWORKS_DB_PATH"] = _dbPath,
                    [ImageStore.DATA_PATH_SETTING] = _dataPath,
                    ["SLIDEWORKS_PUBLIC_URL"] = BaseUrl,
                    [IUserDirectory.ADMIN_EMAIL_SETTING] = TestUser.Ada.Email,
                }));
            builder.ConfigureTestServices(TestAuthScheme.AddTo);
        });
        _factory.UseKestrel(options => options.Listen(IPAddress.Loopback, port));
        _factory.StartServer();
        Services = _factory.Services;

        AssertBundleIsBuilt();
    }

    [OneTimeTearDown]
    public void StopApp()
    {
        _factory.Dispose();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            File.Delete(_dbPath + suffix);
        }
        Directory.Delete(_dataPath, recursive: true);
    }

    // A port nothing is listening on: bind port 0, read what the OS picked, and let it go for Kestrel to take.
    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    // The tests drive the shipped bundle, not a dev server, so it has to exist first.
    private void AssertBundleIsBuilt()
    {
        var webRoot = _factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        File.Exists(Path.Combine(webRoot ?? string.Empty, "index.html")).ShouldBeTrue(
            "The SPA bundle is missing. Build the client first: cd src/SlideWorks.Client && pnpm install && pnpm run build "
            + "(it emits into src/SlideWorks.WebApi/wwwroot).");
    }
}
