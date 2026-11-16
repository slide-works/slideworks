using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace SlideWorks.WebApi.Tests;

public static class TestAppFactory
{
    // In-memory config is added after appsettings.*.json, so these values win
    // over the Development defaults.
    //
    // namedHandlers stubs an outbound HttpClient by its named-client name: the code
    // under test is the production one, only the socket is different, so there is
    // never a test branch in production code. extraConfig carries any further
    // SLIDEWORKS_* values a fixture needs. configureServices runs after Program.cs has
    // registered everything, for a fixture that has to swap one more service the
    // same way. Serilog owns the logger factory and ignores other providers, so a
    // logger is replaced per category rather than added as a provider.
    public static WebApplicationFactory<ApiMarker> Create(
        string dbPath,
        string apiToken,
        FakeTimeProvider clock,
        Dictionary<string, string?>? extraConfig = null,
        Dictionary<string, HttpMessageHandler>? namedHandlers = null,
        Action<IServiceCollection>? configureServices = null)
        => new WebApplicationFactory<ApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SLIDEWORKS_DB_PATH"] = dbPath,
                    ["SLIDEWORKS_API_TOKEN"] = apiToken,
                });
                if (extraConfig != null)
                {
                    config.AddInMemoryCollection(extraConfig);
                }
            });
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
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

    public static void DeleteDatabase(string dbPath)
    {
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            File.Delete(dbPath + suffix);
        }
    }
}
