using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity;

// The anonymous-route matrix (K1) for the routes that exist today, under the app's real Cookies scheme and fallback
// policy. Later owners test their own routes. The test host has no built SPA bundle, so a stand-in one is served from a
// temp folder: without it the static and SPA cases would 404 for a missing file, not pass.
[TestFixture]
public class AnonymousRouteRulesTests : ApiTestFixture
{
    private readonly string _webRoot = CreateWebRoot();

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.Configure<StaticFileOptions>(options => options.FileProvider = new PhysicalFileProvider(_webRoot));

    [OneTimeTearDown]
    public void DeleteWebRoot() => Directory.Delete(_webRoot, recursive: true);

    [TestCase("/health")]
    [TestCase("/assets/app.js")]
    [TestCase("/")]
    [TestCase("/d/abc123DEF456/my-deck")]
    [TestCase("/signin")]
    public async Task Get_WhenAnonymousAndOpenToAnyone_ShouldReturn200(string path)
    {
        var response = await Client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("/api/me")]
    [TestCase("/api/anything")]
    public async Task Get_WhenAnonymousUnderApi_ShouldReturn401Json(string path)
    {
        var response = await Client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Not signed in");
    }

    // The /unsubscribe page is the SPA shell, served to anyone.
    [Test]
    public async Task Get_WhenAnonymousOnUnsubscribePage_ShouldReturn200()
    {
        var response = await Client.GetAsync("/unsubscribe");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Anyone connects to the hub; JoinDeck decides (DeckHubTests).
    [Test]
    public async Task Negotiate_WhenAnonymous_ShouldReturn200()
    {
        var response = await Client.PostAsync("/hubs/deck/negotiate?negotiateVersion=1", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Only the deck hub is open: any other path under /hubs falls under "everything else", so a signed-out caller is
    // refused rather than served the SPA shell.
    [Test]
    public async Task Get_WhenAnonymousUnderUnknownHub_ShouldBeRefused()
    {
        var response = await Client.GetAsync("/hubs/unknown");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
