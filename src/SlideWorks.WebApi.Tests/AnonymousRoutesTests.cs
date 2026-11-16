using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace SlideWorks.WebApi.Tests;

// The routes anyone may reach must stay reachable once sign-in is required everywhere else. This fixture puts a
// "must be signed in" fallback policy and a cookie scheme on the test host, the way sign-in will, and serves a stand-in
// SPA bundle from a temp folder, so the check doesn't depend on the client having been built.
[TestFixture]
public class AnonymousRoutesTests : ApiTestFixture
{
    private readonly string _webRoot = CreateWebRoot();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.Configure<StaticFileOptions>(options => options.FileProvider = new PhysicalFileProvider(_webRoot));
    };

    [OneTimeTearDown]
    public void DeleteWebRoot() => Directory.Delete(_webRoot, recursive: true);

    [TestCase("/health")]
    [TestCase("/d/abc/slug")]
    [TestCase("/assets/app.js")]
    public async Task Get_WhenAnonymousAndSignInRequiredElsewhere_ShouldReturn200(string path)
    {
        using var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await anonymous.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The control: the fallback policy is really in force for an endpoint that isn't marked anonymous.
    [Test]
    public async Task Get_WhenEndpointNotAnonymousAndNotSignedIn_ShouldChallenge()
    {
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TEST_TOKEN);

        var response = await client.GetAsync("/api/anything");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    private static string CreateWebRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slide-works-webroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "index.html"), "<!doctype html><title>SlideWorks</title>");
        File.WriteAllText(Path.Combine(root, "assets", "app.js"), "export {};");
        return root;
    }
}
