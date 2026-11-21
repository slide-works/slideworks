using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity;

// Production runs behind a proxy: the public URL is not the loopback the server listens on. Here the in-memory server
// answers on http://localhost while SLIDEWORKS_PUBLIC_URL is a non-loopback https origin, so every origin-sensitive path
// is checked as production runs it: links, the cross-site write guard, and the session cookie's Secure attribute.
[TestFixture]
public class NonLoopbackPublicUrlTests : ApiTestFixture
{
    private const string LOOPBACK_ORIGIN = "http://localhost";

    protected override string PublicUrl => "https://slides.example.test";

    protected override Action<IServiceCollection>? ConfigureTestServices => SessionProbeController.AddTo;

    [Test]
    public void Links_WhenPublicUrlIsNotLoopback_ShouldUseThePublicOrigin()
    {
        var urls = Factory.Services.GetRequiredService<IPublicUrls>();

        urls.Base.ShouldBe("https://slides.example.test");
        urls.Mcp.ShouldBe("https://slides.example.test/mcp");
        urls.Deck("Ab3dEf6hIj9k", "ship-smaller", 3).ShouldBe("https://slides.example.test/d/Ab3dEf6hIj9k/ship-smaller#3");
    }

    [Test]
    public async Task Post_WhenOriginIsTheLoopback_ShouldReturn403()
    {
        var response = await SignedInWrite(LOOPBACK_ORIGIN);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Post_WhenOriginIsThePublicUrl_ShouldPassTheGuard()
    {
        var response = await SignedInWrite(PublicUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // The raw Set-Cookie header, not the cookie options: the wire is what a browser acts on.
    [Test]
    public async Task Session_WhenPublicUrlIsHttps_ShouldSendSecureOnTheRawHeader()
    {
        using var browser = Factory.CreateClient();

        var response = await browser.GetAsync($"/{SessionProbeController.PATH}");

        var header = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("sw-session=", StringComparison.Ordinal));
        header.Split(';').Select(part => part.Trim().ToLowerInvariant()).ShouldContain("secure");
    }

    private async Task<HttpResponseMessage> SignedInWrite(string origin)
    {
        using var browser = Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/anything") { Content = JsonContent.Create(new { }) };
        request.Headers.Add(TestAuthScheme.HEADER, TestUser.Sam.Email);
        request.Headers.Add("Origin", origin);
        return await browser.SendAsync(request);
    }
}
