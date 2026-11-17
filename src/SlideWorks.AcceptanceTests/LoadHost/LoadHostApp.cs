using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using SlideWorks.WebApi;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.LoadHost;

// The app for a load run (docs/load.md): the production Program.cs and DI on a real Kestrel port, in the Production
// environment (so no per-command database logging), with its own database file and data folder in a fresh temporary
// folder, deleted when it stops. The one addition is the test sign-in scheme, so the seed can act as a person without
// Google, as AcceptanceApp does. It lives outside the SlideWorks.AcceptanceTests namespace so AcceptanceApp's shared app
// does not start alongside it.
public sealed class LoadHostApp : IDisposable
{
    public const int ADDRESSES = 8;

    private readonly string _folder;
    private readonly WebApplicationFactory<ApiMarker> _factory;

    public LoadHostApp(int port)
    {
        _folder = Directory.CreateTempSubdirectory("slide-works-load-").FullName;
        BaseUrl = $"http://127.0.0.1:{port}";
        _factory = new WebApplicationFactory<ApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SLIDEWORKS_DB_PATH"] = Path.Combine(_folder, "slide-works.db"),
                    [ImageStore.DATA_PATH_SETTING] = Path.Combine(_folder, "data"),
                    ["SLIDEWORKS_PUBLIC_URL"] = BaseUrl,
                    [IUserDirectory.ADMIN_EMAIL_SETTING] = TestUser.Ada.Email,
                    ["RenderTokens:Key"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                }));
            builder.ConfigureTestServices(TestAuthScheme.AddTo);
        });
        // Also on 127.0.0.2 to 127.0.0.ADDRESSES, the same app: a client on this machine has only its ephemeral port range
        // (4,096 ports on this WSL host) per destination address and port, so a run of thousands of voters spreads its hub
        // connections over these (the harness's --hub-urls).
        _factory.UseKestrel(options =>
        {
            for (var last = 1; last <= ADDRESSES; last++)
            {
                options.Listen(IPAddress.Parse($"127.0.0.{last}"), port);
            }
        });
        _factory.StartServer();
    }

    public string BaseUrl { get; }

    public IServiceProvider Services => _factory.Services;

    public void Dispose()
    {
        try
        {
            _factory.Dispose();
        }
        finally
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
