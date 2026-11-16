using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity;

// The documented dev loop: the browser is on Vite's http://localhost:3000, and Vite's proxy (changeOrigin) hands Kestrel
// Host: localhost:5510 with the browser's Origin unchanged. A Development host with only appsettings.Development.json's
// SLIDEWORKS_PUBLIC_URL (no test override) must take the Vite origin as its own, or every browser write gets 403.
[TestFixture]
public class DevLoopTests
{
    private const string VITE_ORIGIN = "http://localhost:3000";

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
            builder.ConfigureTestServices(TestAuthScheme.AddTo);
        });
    }

    [OneTimeTearDown]
    public void DisposeHost()
    {
        _factory.Dispose();
        TestAppFactory.DeleteDatabase(_dbPath);
    }

    [Test]
    public async Task Post_WhenSignedInThroughTheViteProxy_ShouldPassTheGuard()
    {
        var response = await ProxiedWrite(VITE_ORIGIN);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // The Kestrel port itself is not the public origin in Development, so a page served from it can't write.
    [Test]
    public async Task Post_WhenOriginIsTheKestrelPort_ShouldReturn403()
    {
        var response = await ProxiedWrite("http://localhost:5510");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> ProxiedWrite(string origin)
    {
        using var proxy = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost:5510") });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/anything") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        request.Headers.Add(TestAuthScheme.HEADER, TestUser.Sam.Email);
        return await proxy.SendAsync(request);
    }
}
