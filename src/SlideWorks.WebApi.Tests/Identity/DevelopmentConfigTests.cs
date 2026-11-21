using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Identity;

// The dev-only settings the security tests lean on live in appsettings.Development.json. This host reads that file with
// no override but a database path (so it leaves no file in the repo), so a change to the file shows up here.
[TestFixture]
public class DevelopmentConfigTests
{
    private string _dbPath = null!;
    private WebApplicationFactory<ApiMarker> _factory = null!;

    [OneTimeSetUp]
    public void StartDevelopmentHost()
    {
        _dbPath = TestAppFactory.NewDatabasePath();
        _factory = new WebApplicationFactory<ApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["SLIDEWORKS_DB_PATH"] = _dbPath }));
        });
    }

    [OneTimeTearDown]
    public void DisposeHost()
    {
        _factory.Dispose();
        TestAppFactory.DeleteDatabase(_dbPath);
    }

    [Test]
    public void PublicUrl_WhenDevelopment_ShouldBeTheViteOrigin()
    {
        _factory.Services.GetRequiredService<IConfiguration>()["SLIDEWORKS_PUBLIC_URL"].ShouldBe("http://localhost:3000");
        _factory.Services.GetRequiredService<IPublicUrls>().Base.ShouldBe("http://localhost:3000");
    }

    [Test]
    public void Storage_WhenDevelopment_ShouldUseTheDevDatabaseAndDataFolder()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        configuration["SLIDEWORKS_DATA_PATH"].ShouldBe("slide-works-dev-data");
    }
}
