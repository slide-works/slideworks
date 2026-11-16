using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Registration;

// Dynamic client registration (RFC 7591, D20): anonymous, public clients only, https or loopback http redirects.
[TestFixture]
public class RegistrationTests : ApiTestFixture
{
    private const string LOOPBACK_REDIRECT = "http://127.0.0.1:33418/callback";

    protected override TestUser SignedInAs => TestUser.Anonymous;

    // Each test starts in a fresh rate-limit window.
    [SetUp]
    public void NextWindow() => Clock.Advance(TimeSpan.FromMinutes(1));

    [TestCase(LOOPBACK_REDIRECT)]
    [TestCase("http://localhost:6274/oauth/callback")]
    [TestCase("http://[::1]:8080/cb")]
    [TestCase("https://claude.ai/api/mcp/auth_callback")]
    public async Task Register_WhenLoopbackRedirect_ShouldReturnClientId(string redirect)
    {
        var response = await RegisterAsync(new { client_name = "Claude", redirect_uris = new[] { redirect } });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonAsync(response);
        body.GetProperty("client_id").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("client_name").GetString().ShouldBe("Claude");
        body.GetProperty("redirect_uris").EnumerateArray().Select(u => u.GetString()).ShouldBe([redirect]);
        body.GetProperty("token_endpoint_auth_method").GetString().ShouldBe("none");
        body.TryGetProperty("client_secret", out _).ShouldBeFalse();
    }

    [TestCase("http://example.com/callback")]
    [TestCase("http://192.168.1.10:8080/callback")]
    [TestCase("https://example.com/callback#fragment")]
    [TestCase("https://user:pass@example.com/callback")]
    [TestCase("javascript:alert(1)")]
    [TestCase("/relative/callback")]
    public async Task Register_WhenHttpNonLoopback_ShouldRefuse(string redirect)
    {
        var response = await RegisterAsync(new { client_name = "Claude", redirect_uris = new[] { redirect } });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_redirect_uri"));
    }

    [Test]
    public async Task Register_WhenMoreThanFiveRedirects_ShouldRefuse()
    {
        var redirects = Enumerable.Range(1, 6).Select(port => $"http://127.0.0.1:{port}/cb").ToArray();

        var response = await RegisterAsync(new { client_name = "Claude", redirect_uris = redirects });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_redirect_uri"));
    }

    [Test]
    public async Task Register_WhenNoRedirects_ShouldRefuse()
    {
        var response = await RegisterAsync(new { client_name = "Claude" });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_redirect_uri"));
    }

    [TestCase(null)]
    [TestCase("   ")]
    public async Task Register_WhenNoClientName_ShouldRefuse(string? name)
    {
        var response = await RegisterAsync(new { client_name = name, redirect_uris = new[] { LOOPBACK_REDIRECT } });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_client_metadata"));
    }

    [Test]
    public async Task Register_WhenClientNameOver60Characters_ShouldRefuse()
    {
        var response = await RegisterAsync(new { client_name = new string('a', 61), redirect_uris = new[] { LOOPBACK_REDIRECT } });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_client_metadata"));
    }

    [Test]
    public async Task Register_WhenOverTenAMinuteFromOneAddress_ShouldReturn429UntilTheWindowPasses()
    {
        var registration = new { client_name = "Claude", redirect_uris = new[] { LOOPBACK_REDIRECT } };
        for (var i = 0; i < 10; i++)
        {
            (await RegisterAsync(registration)).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var refused = await RegisterAsync(registration);
        Clock.Advance(TimeSpan.FromMinutes(1));
        var later = await RegisterAsync(registration);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        later.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private Task<HttpResponseMessage> RegisterAsync(object body) => Client.PostAsJsonAsync("/oauth/register", body);

    // The status and the RFC 7591 error code, after checking the refusal also carries a description.
    private static async Task<(HttpStatusCode Status, string? Error)> RefusalAsync(HttpResponseMessage response)
    {
        var body = await JsonAsync(response);
        body.GetProperty("error_description").GetString().ShouldNotBeNullOrWhiteSpace();
        return (response.StatusCode, body.GetProperty("error").GetString());
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
