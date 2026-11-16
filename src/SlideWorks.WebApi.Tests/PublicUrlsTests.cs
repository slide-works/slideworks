using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests;

[TestFixture]
public class PublicUrlsTests : ApiTestFixture
{
    [Test]
    public void Deck_ShouldBuildHashLink()
    {
        var urls = Factory.Services.GetRequiredService<IPublicUrls>();

        urls.Deck("Ab3dEf6hIj9k", "ship-smaller", 3, 1).ShouldBe("http://localhost/d/Ab3dEf6hIj9k/ship-smaller#3.1");
        urls.Deck("Ab3dEf6hIj9k", "ship-smaller", 3).ShouldBe("http://localhost/d/Ab3dEf6hIj9k/ship-smaller#3");
        urls.Deck("Ab3dEf6hIj9k", "ship-smaller").ShouldBe("http://localhost/d/Ab3dEf6hIj9k/ship-smaller");
        urls.Mcp.ShouldBe("http://localhost/mcp");
    }

    [Test]
    public void Startup_WhenProductionWithoutPublicUrl_ShouldFail()
    {
        var dbPath = TestAppFactory.NewDatabasePath();
        try
        {
            using var factory = HostWithoutPublicUrl(dbPath, "Production");

            var error = Should.Throw<Exception>(() => factory.Services);

            AllMessages(error).ShouldContain("SLIDEWORKS_PUBLIC_URL is required outside Development");
        }
        finally
        {
            TestAppFactory.DeleteDatabase(dbPath);
        }
    }

    [Test]
    public void Base_WhenDevelopmentWithoutPublicUrl_ShouldTrustOnlyALocalHost()
    {
        var dbPath = TestAppFactory.NewDatabasePath();
        try
        {
            using var factory = HostWithoutPublicUrl(dbPath, "Development");
            var urls = factory.Services.GetRequiredService<IPublicUrls>();
            var accessor = factory.Services.GetRequiredService<IHttpContextAccessor>();

            accessor.HttpContext = RequestTo("127.0.0.1:5510");
            urls.Base.ShouldBe("http://127.0.0.1:5510");

            accessor.HttpContext = RequestTo("attacker.example");
            Should.Throw<InvalidOperationException>(() => urls.Base).Message.ShouldContain("SLIDEWORKS_PUBLIC_URL");
        }
        finally
        {
            TestAppFactory.DeleteDatabase(dbPath);
        }
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<ApiMarker> HostWithoutPublicUrl(string dbPath, string environment) =>
        TestAppFactory.Create(dbPath, "token", new FakeTimeProvider(), new() { ["SLIDEWORKS_PUBLIC_URL"] = "" })
            .WithWebHostBuilder(builder => builder.UseEnvironment(environment));

    private static DefaultHttpContext RequestTo(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString(host);
        return context;
    }

    private static string AllMessages(Exception error)
    {
        var messages = new List<string>();
        for (Exception? current = error; current != null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join(" | ", messages);
    }
}
