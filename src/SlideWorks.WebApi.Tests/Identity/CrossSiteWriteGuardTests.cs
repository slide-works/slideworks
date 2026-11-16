using System.Net;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity;

// Every write under /api passes CrossSiteWriteGuard (K1), however the caller was authenticated. No /api write endpoint
// exists yet, so a write that passes the guard reaches the /api fallback's 404, and a refused one gets 403.
[TestFixture]
public class CrossSiteWriteGuardTests : ApiTestFixture
{
    private const string FOREIGN_ORIGIN = "https://attacker.example";

    [Test]
    public async Task Post_WhenForeignOrigin_ShouldReturn403()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.SendAsync(Write(HttpMethod.Post, origin: FOREIGN_ORIGIN));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Post_WhenSameOrigin_ShouldPass()
    {
        var response = await Client.SendAsync(Write(HttpMethod.Post, origin: TestAppFactory.PUBLIC_URL));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // The test sign-in is a scheme of its own, and the guard still applies to it. Every method but GET, HEAD, OPTIONS
    // and TRACE is guarded, including ones nothing routes today (MERGE, PROPPATCH).
    [TestCase("POST")]
    [TestCase("PUT")]
    [TestCase("PATCH")]
    [TestCase("DELETE")]
    [TestCase("MERGE")]
    [TestCase("PROPPATCH")]
    public async Task Post_WhenTestSchemeAndForeignOrigin_ShouldReturn403(string method)
    {
        var response = await Client.SendAsync(Write(new HttpMethod(method), origin: FOREIGN_ORIGIN));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // A sandboxed frame or a privacy-preserving redirect sends "Origin: null", which is never SlideWorks.
    [Test]
    public async Task Post_WhenOriginIsNull_ShouldReturn403()
    {
        var response = await Client.SendAsync(Write(HttpMethod.Post, origin: "null"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Post_WhenSignedInWithNoOriginAndNoFetchSite_ShouldReturn403()
    {
        using var bare = Factory.CreateClient();
        bare.DefaultRequestHeaders.Add(TestAuthScheme.HEADER, TestUser.Sam.Email);

        var response = await bare.SendAsync(Write(HttpMethod.Post, origin: null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Post_WhenSignedInWithNoOriginButSameOriginFetchSite_ShouldPass()
    {
        using var bare = Factory.CreateClient();
        bare.DefaultRequestHeaders.Add(TestAuthScheme.HEADER, TestUser.Sam.Email);
        var request = Write(HttpMethod.Post, origin: null);
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        var response = await bare.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // A sibling subdomain is same-site but not same-origin, so it gets no pass.
    [Test]
    public async Task Post_WhenSignedInWithNoOriginAndSameSiteFetchSite_ShouldReturn403()
    {
        using var bare = Factory.CreateClient();
        bare.DefaultRequestHeaders.Add(TestAuthScheme.HEADER, TestUser.Sam.Email);
        var request = Write(HttpMethod.Post, origin: null);
        request.Headers.Add("Sec-Fetch-Site", "same-site");

        var response = await bare.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // An origin that only starts with SlideWorks' own is another site.
    [TestCase("http://localhost.attacker.example")]
    [TestCase("http://localhost:8080")]
    public async Task Post_WhenOriginOnlyStartsWithOurs_ShouldReturn403(string origin)
    {
        var response = await Client.SendAsync(Write(HttpMethod.Post, origin: origin));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Post_WhenSignedInWithNoOriginAndCrossSiteFetchSite_ShouldReturn403()
    {
        using var bare = Factory.CreateClient();
        bare.DefaultRequestHeaders.Add(TestAuthScheme.HEADER, TestUser.Sam.Email);
        var request = Write(HttpMethod.Post, origin: null);
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        var response = await bare.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // With no Origin and no session there is nothing for another site to ride on (an upload by token is the case K1
    // names), so the guard lets it through to authorization, which refuses it here because no such endpoint is open.
    [Test]
    public async Task Put_WhenAnonymousWithNoOrigin_ShouldPassTheGuard()
    {
        using var bare = Factory.CreateClient();

        var response = await bare.SendAsync(Write(HttpMethod.Put, origin: null, path: "/api/uploads/some-token"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Get_WhenForeignOrigin_ShouldNotBeGuarded()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/anything");
        request.Headers.Add("Origin", FOREIGN_ORIGIN);

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // /oauth/* (form posts from agents) and /mcp (bearer tokens) are outside /api, so outside the guard.
    [TestCase("/oauth/token")]
    [TestCase("/mcp")]
    public async Task Post_WhenOutsideApiWithForeignOrigin_ShouldNotBeRefusedByTheGuard(string path)
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.SendAsync(Write(HttpMethod.Post, origin: FOREIGN_ORIGIN, path: path));

        (await response.Content.ReadAsStringAsync()).ShouldNotContain("Cross-site request refused");
    }

    private static HttpRequestMessage Write(HttpMethod method, string? origin, string path = "/api/anything")
    {
        var request = new HttpRequestMessage(method, path) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        if (origin != null)
        {
            request.Headers.Remove("Origin");
            request.Headers.Add("Origin", origin);
        }
        return request;
    }
}
