using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join;

// The join host (J7): with SLIDEWORKS_JOIN_URL set, a request whose Host is the join host is served only the join paths, and
// the rest goes to the public URL. The test server answers on http://localhost, the public URL, and a request names the join
// host by its absolute URL.
[TestFixture]
public class JoinHostTests : ApiTestFixture
{
    private const string JOIN = "http://join.example";
    private const string PUBLIC = TestAppFactory.PUBLIC_URL;

    protected override Dictionary<string, string?> ExtraConfig => new() { ["SLIDEWORKS_JOIN_URL"] = JOIN };

    protected override Action<IServiceCollection>? ConfigureTestServices => SessionProbeController.AddTo;

    [Test]
    public async Task JoinHost_WhenRoot_ShouldRedirectToJoin()
    {
        using var client = ClientFor(TestUser.Anonymous);

        var response = await client.GetAsync($"{JOIN}/");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/join");
    }

    [TestCase("/d/abc/slug", "")]
    [TestCase("/decks", "?page=2")]
    [TestCase("/api/decks", "")]
    [TestCase("/signin", "")]
    public async Task JoinHost_WhenAppPath_ShouldRedirectToPublicUrl(string path, string query)
    {
        using var client = ClientFor(TestUser.Anonymous);

        var response = await client.GetAsync($"{JOIN}{path}{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe($"{PUBLIC}{path}{query}");
    }

    [TestCase("/health")]
    [TestCase("/api/join/999999")]
    [TestCase("/api/images/abc")]
    [TestCase("/hubs/deck/negotiate")]
    [TestCase("/api/join/not-a-code")]
    [TestCase("/join")]
    [TestCase("/j/482913")]
    [TestCase("/482913")]
    [TestCase("/assets/app.js")]
    [TestCase("/favicon.svg")]
    public async Task JoinHost_WhenJoinPath_ShouldBeServed(string path)
    {
        using var client = ClientFor(TestUser.Anonymous);

        var response = await client.GetAsync($"{JOIN}{path}");

        response.StatusCode.ShouldNotBe(HttpStatusCode.Redirect);
    }

    [TestCase("/4829")]
    [TestCase("/4829131")]
    [TestCase("/48291a")]
    [TestCase("/joiner")]
    public async Task JoinHost_WhenNotSixDigitsOrJoin_ShouldRedirectToPublicUrl(string path)
    {
        using var client = ClientFor(TestUser.Anonymous);

        var response = await client.GetAsync($"{JOIN}{path}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    // Only the two configured origins are ever chosen: a forged Host, or X-Forwarded-Host naming the join host, gets the public
    // origin; and the join origin is then refused by the write guard on the hub.
    [Test]
    public async Task JoinHost_WhenForgedHost_ShouldOnlyPickConfiguredOrigin()
    {
        using var client = ClientFor(TestUser.Anonymous);
        var forgedHost = await client.GetFromJsonAsync<RequestOriginDto>("http://attacker.example/test/session-probe");
        using var forwarded = new HttpRequestMessage(HttpMethod.Get, $"{PUBLIC}/test/session-probe");
        forwarded.Headers.Add("X-Forwarded-Host", "join.example");
        using var forwardedResponse = await client.SendAsync(forwarded);

        var seen = await forwardedResponse.Content.ReadFromJsonAsync<RequestOriginDto>();

        forgedHost.ShouldBe(new RequestOriginDto("http", "localhost"));
        seen.ShouldBe(new RequestOriginDto("http", "localhost"));
        (await Hub("http://attacker.example", JOIN)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HubViaForwardedHost(JOIN)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> Hub(string host, string origin)
    {
        using var client = Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{host}/hubs/deck?id=not-a-connection");
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> HubViaForwardedHost(string origin)
    {
        using var client = Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{PUBLIC}/hubs/deck?id=not-a-connection");
        request.Headers.Add("X-Forwarded-Host", "join.example");
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request);
    }
}
