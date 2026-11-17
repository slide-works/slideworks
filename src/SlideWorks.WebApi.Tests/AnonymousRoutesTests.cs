using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Render;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests;

// The routes anyone may reach must stay reachable once sign-in is required everywhere else. This fixture puts a
// "must be signed in" fallback policy on the test host, the way sign-in will, and serves a stand-in SPA bundle from a
// temp folder, so the check doesn't depend on the client having been built. The policy authenticates through a scheme
// of this fixture's own, which leaves the default scheme alone, so the test holds whether or not the app registers
// its own schemes and policies.
[TestFixture]
public class AnonymousRoutesTests : ApiTestFixture
{
    private const string TEST_SCHEME = "AnonymousRoutesTest";

    private readonly string _webRoot = CreateWebRoot();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddAuthentication().AddCookie(TEST_SCHEME);
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(TEST_SCHEME).RequireAuthenticatedUser().Build());
        services.Configure<StaticFileOptions>(options => options.FileProvider = new PhysicalFileProvider(_webRoot));
    };

    [OneTimeTearDown]
    public void DeleteWebRoot() => Directory.Delete(_webRoot, recursive: true);

    [TestCase("/health")]
    [TestCase("/d/abc/slug")]
    [TestCase("/assets/app.js")]
    [TestCase("/d/abc")]
    [TestCase("/api/cards/abc.png")]
    public async Task Get_WhenAnonymousAndSignInRequiredElsewhere_ShouldReturn200(string path)
    {
        using var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await anonymous.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("/api/cards/abc.png")]
    public async Task Head_WhenAnonymousAndSignInRequiredElsewhere_ShouldReturn200(string path)
    {
        using var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await anonymous.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The control: the fallback policy is really in force for an endpoint that isn't marked anonymous. Whether the
    // refusal is a challenge redirect or a 401 depends on which scheme answers it, so either counts. A 200, or the /api
    // fallback's 404, would mean the policy isn't in force.
    [Test]
    public async Task Get_WhenEndpointNotAnonymousAndNotSignedIn_ShouldBeRefused()
    {
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/anything");

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    // L9: a headless render page holds only a render token. Trading it for the image cookie, and reading its deck's
    // image with it, both work with sign-in required everywhere else.
    [Test]
    public async Task PostRenderSession_WhenAnonymousWithRenderToken_ShouldSetTheCookie()
    {
        using var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (deck, _) = await SeedPrivateImageAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/render/{deck}/session");
        request.Headers.Add(RenderSessions.HEADER, Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, RenderPurpose.Preview));

        using var response = await anonymous.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").ShouldContain(cookie => cookie.StartsWith(RenderSessions.COOKIE + "="));
    }

    [Test]
    public async Task GetImage_WhenAnonymousWithRenderToken_ShouldReturn200()
    {
        using var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (deck, image) = await SeedPrivateImageAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/images/{image}");
        request.Headers.Add(RenderSessions.HEADER, Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, RenderPurpose.Card));

        using var response = await anonymous.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // A private deck with one image, made the way an agent makes them.
    private async Task<(string Deck, string Image)> SeedPrivateImageAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        return (deck, await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png")));
    }

    // With no SLIDEWORKS_JOIN_URL there is no join host: the app host serves the keypad and a join link itself (J7).
    [TestCase("/join")]
    [TestCase("/j/482913")]
    public async Task NoJoinUrl_WhenAppHost_ShouldServeJoinAndJ(string path)
    {
        using var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await anonymous.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("SlideWorks");
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
