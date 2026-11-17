using System.Net;
using System.Net.Http.Json;
using SlideWorks.WebApi.Tests.Dtos;
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
    // names), so the guard lets it through to the anonymous upload endpoint, which refuses the unknown token itself.
    [Test]
    public async Task Put_WhenAnonymousWithNoOrigin_ShouldPassTheGuard()
    {
        using var bare = Factory.CreateClient();

        var response = await bare.SendAsync(Write(HttpMethod.Put, origin: null, path: "/api/uploads/some-token"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Upload not found");
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

    // The audience hub is reached over Server-Sent Events and long polling too (D7). Each is a plain GET on /hubs/deck, so
    // the guard that refuses another site's negotiate and WebSocket upgrade must refuse these as well, and a request from
    // our own origin or with none must reach the hub.
    [Test]
    public async Task CrossSiteWriteGuard_WhenForeignOriginOnSse_ShouldRefuse()
    {
        using var client = Factory.CreateClient();

        var foreign = await client.SendAsync(Transport("text/event-stream", FOREIGN_ORIGIN));
        var own = await client.SendAsync(Transport("text/event-stream", TestAppFactory.PUBLIC_URL));

        foreign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        own.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CrossSiteWriteGuard_WhenForeignOriginOnLongPolling_ShouldRefuse()
    {
        using var client = Factory.CreateClient();

        var foreign = await client.SendAsync(Transport(accept: null, FOREIGN_ORIGIN));
        var own = await client.SendAsync(Transport(accept: null, TestAppFactory.PUBLIC_URL));

        foreign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        own.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    private static HttpRequestMessage Transport(string? accept, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/hubs/deck?id=not-a-connection");
        if (accept != null)
        {
            request.Headers.Add("Accept", accept);
        }
        request.Headers.Add("Origin", origin);
        return request;
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
