using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Authorize;

// GET /oauth/authorize (RFC 6749 4.1, PKCE S256 only, one resource and one scope). An unknown client or redirect URI is a
// 400 page and never a redirect; a protocol error goes back to the client as error=; then signed out goes to sign in,
// pending goes to /pending, and an approved presenter goes to the consent page.
[TestFixture]
public class AuthorizeTests : ApiTestFixture
{
    private string _clientId = null!;

    [OneTimeSetUp]
    public async Task RegisterClient()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        _clientId = await OAuthFlow.RegisterAsync(anonymous);
    }

    [Test]
    public async Task Authorize_WhenSignedOut_ShouldRedirectToSignInKeepingFullQuery()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        var path = OAuthFlow.AuthorizePath(_clientId);

        var response = await anonymous.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location.ShouldNotBeNull().OriginalString;
        location.ShouldBe("/signin?returnTo=" + Uri.EscapeDataString(path));
        Uri.UnescapeDataString(location["/signin?returnTo=".Length..]).ShouldBe(path);
    }

    [Test]
    public async Task Authorize_WhenSignedIn_ShouldRedirectToConsent()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);

        requestId.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Authorize_WhenPending_ShouldRedirectToPending()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync(OAuthFlow.AuthorizePath(_clientId));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe("/pending");
    }

    [TestCase("http://127.0.0.1:33418/other")]
    [TestCase("http://127.0.0.1:33418/callback/")]
    [TestCase("https://evil.example/callback")]
    [TestCase(null)]
    public async Task Authorize_WhenRedirectMismatch_ShouldShow400NotRedirect(string? redirect)
    {
        var response = await Client.GetAsync(OAuthFlow.AuthorizePath(_clientId, ("redirect_uri", redirect)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Location.ShouldBeNull();
    }

    // RFC 8252 7.3: a loopback redirect matches on any port, and the request keeps the port the agent sent.
    [TestCase("http://127.0.0.1:50123/callback", "127.0.0.1:50123")]
    [TestCase("http://127.0.0.1/callback", "127.0.0.1")]
    public async Task Authorize_WhenLoopbackRedirectOnAnotherPort_ShouldRedirectToConsent(string redirect, string returnsTo)
    {
        var response = await Client.GetAsync(OAuthFlow.AuthorizePath(_clientId, ("redirect_uri", redirect)));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location.ShouldNotBeNull().OriginalString;
        location.ShouldStartWith("/oauth/consent?request=", Case.Sensitive);
        var consentPath = "/api/oauth/consent/" + Uri.EscapeDataString(location["/oauth/consent?request=".Length..]);
        var consent = JsonDocument.Parse(await (await Client.GetAsync(consentPath)).Content.ReadAsStringAsync()).RootElement;
        consent.GetProperty("returnsTo").GetString().ShouldBe(returnsTo);
        var decision = await Client.PostAsJsonAsync(consentPath, new { approve = true });
        var back = JsonDocument.Parse(await decision.Content.ReadAsStringAsync()).RootElement.GetProperty("redirect").GetString();
        back.ShouldNotBeNull().ShouldStartWith(redirect + "?code=", Case.Sensitive);
    }

    // Only the port may differ: another loopback host or path is still a mismatch.
    [TestCase("http://localhost:33418/callback")]
    [TestCase("http://127.0.0.1:50123/other")]
    [TestCase("http://127.0.0.1:50123/callback?extra=1")]
    [TestCase("https://127.0.0.1:33418/callback")]
    public async Task Authorize_WhenLoopbackRedirectDiffersBeyondThePort_ShouldShow400NotRedirect(string redirect)
    {
        var response = await Client.GetAsync(OAuthFlow.AuthorizePath(_clientId, ("redirect_uri", redirect)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Location.ShouldBeNull();
    }

    // Off loopback the match stays exact, port included.
    [Test]
    public async Task Authorize_WhenNonLoopbackRedirectOnAnotherPort_ShouldShow400NotRedirect()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        var registered = await anonymous.PostAsJsonAsync("/oauth/register", new { client_name = "Web agent", redirect_uris = new[] { "https://agent.example/callback" } });
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        var clientId = JsonDocument.Parse(await registered.Content.ReadAsStringAsync()).RootElement.GetProperty("client_id").GetString().ShouldNotBeNull();

        var response = await Client.GetAsync(OAuthFlow.AuthorizePath(clientId, ("redirect_uri", "https://agent.example:8443/callback")));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Location.ShouldBeNull();
    }

    [TestCase("unknown-client")]
    [TestCase(null)]
    public async Task Authorize_WhenUnknownClient_ShouldShow400NotRedirect(string? clientId)
    {
        var response = await Client.GetAsync(OAuthFlow.AuthorizePath(_clientId, ("client_id", clientId)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Location.ShouldBeNull();
    }

    [TestCase("code_challenge_method", "plain")]
    [TestCase("code_challenge_method", null)]
    [TestCase("code_challenge", null)]
    [TestCase("code_challenge", "too-short")]
    public async Task Authorize_WhenNoS256_ShouldRedirectWithError(string name, string? value)
    {
        var query = await ErrorRedirectAsync(OAuthFlow.AuthorizePath(_clientId, (name, value)));

        query["error"].ShouldBe("invalid_request");
        query["state"].ShouldBe(OAuthFlow.STATE);
    }

    [TestCase("https://elsewhere.example/mcp")]
    [TestCase("http://localhost/mcp/")]
    [TestCase("http://localhost")]
    public async Task Authorize_WhenWrongResource_ShouldRedirectWithInvalidTarget(string resource)
    {
        var query = await ErrorRedirectAsync(OAuthFlow.AuthorizePath(_clientId, ("resource", resource)));

        query["error"].ShouldBe("invalid_target");
        query["state"].ShouldBe(OAuthFlow.STATE);
    }

    [TestCase("decks admin")]
    [TestCase("openid")]
    public async Task Authorize_WhenUnknownScope_ShouldRedirectWithInvalidScope(string scope)
    {
        var query = await ErrorRedirectAsync(OAuthFlow.AuthorizePath(_clientId, ("scope", scope)));

        query["error"].ShouldBe("invalid_scope");
    }

    [Test]
    public async Task Authorize_WhenResponseTypeIsToken_ShouldRedirectWithUnsupportedResponseType()
    {
        var query = await ErrorRedirectAsync(OAuthFlow.AuthorizePath(_clientId, ("response_type", "token")));

        query["error"].ShouldBe("unsupported_response_type");
    }

    // A client that leaves scope and resource out gets the one scope and the one resource there are.
    [Test]
    public async Task Authorize_WhenScopeAndResourceOmitted_ShouldRedirectToConsentForDecks()
    {
        var response = await Client.GetAsync(OAuthFlow.AuthorizePath(_clientId, ("scope", null), ("resource", null)));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location.ShouldNotBeNull().OriginalString;
        location.ShouldStartWith("/oauth/consent?request=", Case.Sensitive);
        var consent = await Client.GetAsync("/api/oauth/consent/" + Uri.EscapeDataString(location["/oauth/consent?request=".Length..]));
        JsonDocument.Parse(await consent.Content.ReadAsStringAsync()).RootElement.GetProperty("scope").GetString().ShouldBe("decks");
    }

    // A protocol error is checked before sign-in, so a signed-out browser gets it too, without signing in first.
    [Test]
    public async Task Authorize_WhenSignedOutAndNoS256_ShouldRedirectWithErrorNotSignIn()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync(OAuthFlow.AuthorizePath(_clientId, ("code_challenge_method", "plain")));

        OAuthFlow.CallbackQuery(response.Headers.Location.ShouldNotBeNull().OriginalString)["error"].ShouldBe("invalid_request");
    }

    private async Task<IReadOnlyDictionary<string, string>> ErrorRedirectAsync(string path)
    {
        var response = await Client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var query = OAuthFlow.CallbackQuery(response.Headers.Location.ShouldNotBeNull().OriginalString);
        query.ContainsKey("code").ShouldBeFalse();
        return query;
    }
}
