using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests;

public static class TestAppFactory
{
    // SLIDEWORKS_PUBLIC_URL on every test host: the origin the test server answers on, so links, OAuth metadata and the
    // cross-site write guard all agree with the requests the tests send.
    public const string PUBLIC_URL = "http://localhost";

    // In-memory config is added after appsettings.*.json, so these values win
    // over the Development defaults. SLIDEWORKS_ADMIN_EMAIL is Ada's, so she is the admin from her first sign-in; a
    // fixture overrides it through extraConfig (to "" for a server with no admin).
    //
    // namedHandlers stubs an outbound HttpClient by its named-client name: the code
    // under test is the production one, only the socket is different, so there is
    // never a test branch in production code. extraConfig carries any further
    // SLIDEWORKS_* values a fixture needs. configureServices runs after Program.cs has
    // registered everything, for a fixture that has to swap one more service the
    // same way. publicUrl is a fixture's own SLIDEWORKS_PUBLIC_URL, such as a non-loopback https origin. Every host also gets TestAuthScheme, so a request signs in by naming a TestUser. Serilog owns the logger factory and ignores other providers, so a
    // logger is replaced per category rather than added as a provider.
    public static WebApplicationFactory<ApiMarker> Create(
        string dbPath,
        FakeTimeProvider clock,
        Dictionary<string, string?>? extraConfig = null,
        Dictionary<string, HttpMessageHandler>? namedHandlers = null,
        Action<IServiceCollection>? configureServices = null,
        string publicUrl = PUBLIC_URL)
        => new WebApplicationFactory<ApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SLIDEWORKS_DB_PATH"] = dbPath,
                    ["SLIDEWORKS_DATA_PATH"] = DataPathFor(dbPath),
                    ["SLIDEWORKS_PUBLIC_URL"] = publicUrl,
                    [IUserDirectory.ADMIN_EMAIL_SETTING] = TestUser.Ada.Email,
                });
                if (extraConfig != null)
                {
                    config.AddInMemoryCollection(extraConfig);
                }
            });
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
            builder.ConfigureTestServices(TestAuthScheme.AddTo);
            builder.ConfigureTestServices(services => services.AddSingleton<FakeRenderEngine>().AddSingleton<IRenderEngine>(provider => provider.GetRequiredService<FakeRenderEngine>()));
            foreach (var (name, handler) in namedHandlers ?? [])
            {
                builder.ConfigureServices(services => services
                    .AddHttpClient(name)
                    .ConfigurePrimaryHttpMessageHandler(() => handler));
            }
            if (configureServices != null)
            {
                builder.ConfigureServices(configureServices);
            }
        });

    public static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"slide-works-tests-{Guid.NewGuid():N}.db");

    // The data folder (uploaded images) of the host whose database is dbPath, next to it in the temp folder.
    public static string DataPathFor(string dbPath) => dbPath + "-data";

    public static void DeleteDatabase(string dbPath)
    {
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            File.Delete(dbPath + suffix);
        }
        var dataPath = DataPathFor(dbPath);
        if (Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }
}
