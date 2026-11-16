using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity;

// Behind a TLS proxy the app itself serves plain HTTP. With an https SLIDEWORKS_PUBLIC_URL, PublicOriginMiddleware makes
// that URL the scheme and host of every request, so the session cookie is Secure and a forged Host header changes
// nothing. The test server answers on http://localhost, like the container behind its proxy.
[TestFixture]
public class PublicOriginTests : ApiTestFixture
{
    private const string PUBLIC_URL = "https://slides.example";
    private const string FORGED_HOST = "http://attacker.example";

    protected override Dictionary<string, string?> ExtraConfig => new() { ["SLIDEWORKS_PUBLIC_URL"] = PUBLIC_URL };

    protected override Action<IServiceCollection>? ConfigureTestServices => SessionProbeController.AddTo;

    [Test]
    public async Task Session_WhenPublicUrlIsHttps_ShouldBeSecureHttpOnlyLax()
    {
        using var browser = Factory.CreateClient();

        var response = await browser.GetAsync($"/{SessionProbeController.PATH}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SessionCookies.Attributes(response).ShouldBe(["path=/", "secure", "samesite=lax", "httponly"], ignoreOrder: true);
    }

    [Test]
    public async Task Request_WhenHostHeaderIsForged_ShouldUseThePublicSchemeAndHost()
    {
        using var browser = Factory.CreateClient();

        var response = await browser.GetAsync($"{FORGED_HOST}/{SessionProbeController.PATH}");

        var seen = (await response.Content.ReadFromJsonAsync<RequestOriginDto>())!;
        seen.ShouldBe(new RequestOriginDto("https", "slides.example"));
        SessionCookies.Attributes(response).ShouldContain("secure");
    }

    // The write guard compares Origin with the public URL, not with whatever Host the request names.
    [Test]
    public async Task Post_WhenOriginMatchesForgedHost_ShouldReturn403()
    {
        var response = await SignedInWrite($"{FORGED_HOST}/api/anything", origin: FORGED_HOST);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Post_WhenOriginIsThePublicUrl_ShouldPassTheGuard()
    {
        var response = await SignedInWrite("/api/anything", origin: PUBLIC_URL);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> SignedInWrite(string url, string origin)
    {
        using var browser = Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { }) };
        request.Headers.Add(TestAuthScheme.HEADER, TestUser.Sam.Email);
        request.Headers.Add("Origin", origin);
        return await browser.SendAsync(request);
    }
}
