using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Registration;

// Dynamic client registration (RFC 7591, D20): anonymous, public clients only. Redirects (D4): https, loopback http, or an
// app's private-use scheme that is reverse-DNS or allow-listed (cursor); never a script, file or other refused scheme.
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
    [TestCase("http://localhost.evil.example/callback")]
    [TestCase("http://127.0.0.2:8080/callback")]
    [TestCase("https://example.com/callback#fragment")]
    [TestCase("https://user:pass@example.com/callback")]
    [TestCase("cursor://user@anysphere.cursor-mcp/oauth/callback")]
    [TestCase("https://example.com/call back")]
    [TestCase("https://example.com/call\tback")]
    [TestCase("javascript:alert(1)")]
    [TestCase("/relative/callback")]
    [TestCase("relative/callback")]
    // Not printable ASCII: a Unicode host can pass for another one on the consent page while the browser goes to its
    // punycode (a Cyrillic "а", an ideographic full stop, a zero-width space). Real agents' redirect URIs are ASCII.
    [TestCase("https://\u0430pple.com/cb")]
    [TestCase("https://good.example\u3002evil.example/cb")]
    [TestCase("https://good\u200B.example/cb")]
    [TestCase("http://localhost:6274/caf\u00E9")]
    public async Task Register_WhenHttpNonLoopback_ShouldRefuse(string redirect)
    {
        var response = await RegisterAsync(new { client_name = "Claude", redirect_uris = new[] { redirect } });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_redirect_uri"));
    }

    [Test]
    public async Task Register_WhenPunycodeHost_ShouldReturnClientId()
    {
        var response = await RegisterAsync(new { client_name = "Claude", redirect_uris = new[] { "https://xn--pple-43d.com/cb" } });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task Register_WhenLongerThan2000Characters_ShouldRefuse()
    {
        var redirect = "https://example.com/" + new string('a', 2000 - "https://example.com/".Length + 1);

        var response = await RegisterAsync(new { client_name = "Claude", redirect_uris = new[] { redirect } });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_redirect_uri"));
    }

    // Cursor's IDE registers its app scheme together with two web callbacks, in one registration.
    [Test]
    public async Task Register_WhenCursorsExactBody_ShouldReturnClientIdWithAllThreeRedirects()
    {
        string[] redirects =
        [
            "cursor://anysphere.cursor-mcp/oauth/callback",
            "https://www.cursor.com/agents/mcp/oauth/callback",
            "http://localhost:8787/callback",
        ];

        var response = await RegisterAsync(new
        {
            client_name = "Cursor",
            redirect_uris = redirects,
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "none",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonAsync(response);
        body.GetProperty("client_id").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("redirect_uris").EnumerateArray().Select(u => u.GetString()).ShouldBe(redirects);
    }

    [TestCase("com.example.app:/oauth/callback")]
    [TestCase("com.example.app://callback")]
    [TestCase("cursor://anysphere.cursor-mcp/oauth/callback")]
    public async Task Register_WhenPrivateUseSchemeIsReverseDnsOrAllowed_ShouldReturnClientId(string redirect)
    {
        var response = await RegisterAsync(new { client_name = "App", redirect_uris = new[] { redirect } });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // Each refused scheme refuses the registration, even next to a good redirect.
    [TestCase("javascript://example.com/%0Aalert(1)")]
    [TestCase("data:text/html,hello")]
    [TestCase("file:///etc/passwd")]
    [TestCase("vbscript:msgbox(1)")]
    [TestCase("blob:https://example.com/0b6f")]
    [TestCase("about:blank")]
    [TestCase("filesystem:https://example.com/temporary/x")]
    [TestCase("ws://localhost:8080/callback")]
    [TestCase("wss://example.com/callback")]
    [TestCase("ftp://example.com/callback")]
    [TestCase("mailto:someone@example.com")]
    [TestCase("tel:+15550100")]
    [TestCase("JavaScript:alert(1)")]
    public async Task Register_WhenRefusedScheme_ShouldRefuse(string redirect)
    {
        var response = await RegisterAsync(new { client_name = "Claude", redirect_uris = new[] { LOOPBACK_REDIRECT, redirect } });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_redirect_uri"));
    }

    [TestCase("myapp://callback")]
    [TestCase("myapp:/callback")]
    [TestCase("Cursor://anysphere.cursor-mcp/oauth/callback")]
    [TestCase("Com.Example.App:/callback")]
    public async Task Register_WhenPrivateUseSchemeHasNoDotAndIsNotAllowed_ShouldRefuse(string redirect)
    {
        var response = await RegisterAsync(new { client_name = "App", redirect_uris = new[] { redirect } });

        (await RefusalAsync(response)).ShouldBe((HttpStatusCode.BadRequest, "invalid_redirect_uri"));
    }

    // One bad URI refuses the whole registration, even next to good ones.
    [Test]
    public async Task Register_WhenOneOfSeveralRedirectsIsRefused_ShouldRefuseAll()
    {
        var response = await RegisterAsync(new
        {
            client_name = "Cursor",
            redirect_uris = new[] { "cursor://anysphere.cursor-mcp/oauth/callback", "http://evil.example/callback" },
        });

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

    // RFC 7591 lets the server replace metadata: a long name is cut to 60 characters, not refused.
    [Test]
    public async Task Register_WhenClientNameOver60Characters_ShouldTruncateIt()
    {
        var name = "Claude Code (" + new string('s', 60) + ")";

        var response = await RegisterAsync(new { client_name = name, redirect_uris = new[] { LOOPBACK_REDIRECT } });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await JsonAsync(response)).GetProperty("client_name").GetString().ShouldBe(name[..60]);
    }

    [Test]
    public async Task Register_WhenClientNameHasControlCharacter_ShouldRefuse()
    {
        var response = await RegisterAsync(new { client_name = "Claude\u0007", redirect_uris = new[] { LOOPBACK_REDIRECT } });

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
        refused.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldBe(TimeSpan.FromMinutes(1));
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
