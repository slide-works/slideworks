using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.OAuth.Authorize;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Token;

// POST /oauth/token and POST /oauth/revoke (D20): a code becomes an access and refresh token once, bound to its client,
// redirect URI, PKCE verifier and resource; refresh rotates and a replay ends the family; revoke ends it too, and /mcp
// stops taking the token. Every response is no-store, and every refusal is an RFC 6749 error body.
[TestFixture]
public class TokenTests : ApiTestFixture
{
    private HttpClient _anonymous = null!;
    private TokenEndpoint _endpoint = null!;
    private string _clientId = null!;
    private string _otherClientId = null!;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    [OneTimeSetUp]
    public async Task RegisterClients()
    {
        _anonymous = ClientFor(TestUser.Anonymous);
        _endpoint = new TokenEndpoint(Factory, _anonymous);
        _clientId = await OAuthFlow.RegisterAsync(_anonymous, "Claude");
        _otherClientId = await OAuthFlow.RegisterAsync(_anonymous, "Another Agent");
    }

    [OneTimeTearDown]
    public void DisposeAnonymous() => _anonymous.Dispose();

    // Each test starts in a fresh rate-limit window: every request here comes from the same address.
    [SetUp]
    public void NextWindow() => Clock.Advance(TimeSpan.FromMinutes(1));

    [Test]
    public async Task Token_WhenValidCodeAndVerifier_ShouldReturnAccessAndRefresh()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _endpoint.RedeemAsync(_clientId, code);

        var body = await TokenEndpoint.OkBodyAsync(response);
        body.GetProperty("token_type").GetString().ShouldBe("Bearer");
        body.GetProperty("expires_in").GetInt32().ShouldBe(3600);
        body.GetProperty("scope").GetString().ShouldBe("decks");
        var access = body.GetProperty("access_token").GetString().ShouldNotBeNull();
        var refresh = body.GetProperty("refresh_token").GetString().ShouldNotBeNull();
        access.Length.ShouldBeGreaterThanOrEqualTo(43);
        refresh.ShouldNotBe(access);
        var agent = await ValidateAsync(access);
        agent.ShouldNotBeNull();
        agent.Email.ShouldBe(TestUser.Sam.Email);
        agent.ClientName.ShouldBe("Claude");
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);
    }

    // A client that leaves resource out gets the one resource there is (RFC 8707), as /oauth/authorize does.
    [Test]
    public async Task Token_WhenResourceLeftOut_ShouldBindToMcp()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var body = await TokenEndpoint.OkBodyAsync(await _endpoint.RedeemAsync(_clientId, code, ("resource", null)));

        (await _endpoint.McpStatusAsync(body.GetProperty("access_token").GetString()!)).ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_WhenWrongVerifier_ShouldReturnInvalidGrant()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _endpoint.RedeemAsync(_clientId, code, ("code_verifier", new string('a', 43)));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Token_WhenWrongClient_ShouldReturnInvalidGrant()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _endpoint.RedeemAsync(_otherClientId, code);

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Token_WhenWrongRedirect_ShouldReturnInvalidGrant()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _endpoint.RedeemAsync(_clientId, code, ("redirect_uri", "http://127.0.0.1:33418/elsewhere"));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Token_WhenWrongResource_ShouldReturnInvalidTarget()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _endpoint.RedeemAsync(_clientId, code, ("resource", "https://elsewhere.example/mcp"));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_target");
    }

    [Test]
    public async Task Token_WhenUnknownClient_ShouldReturnInvalidClient()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _endpoint.RedeemAsync("no-such-client", code);

        (await TokenEndpoint.ErrorAsync(response, HttpStatusCode.Unauthorized)).ShouldBe("invalid_client");
    }

    [TestCase("code")]
    [TestCase("client_id")]
    [TestCase("redirect_uri")]
    [TestCase("code_verifier")]
    [TestCase("grant_type")]
    public async Task Token_WhenParameterMissing_ShouldReturnInvalidRequest(string parameter)
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _endpoint.RedeemAsync(_clientId, code, (parameter, null));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_request");
    }

    [Test]
    public async Task Token_WhenGrantTypeUnsupported_ShouldReturnUnsupportedGrantType()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _endpoint.RedeemAsync(_clientId, code, ("grant_type", "password"));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("unsupported_grant_type");
    }

    // RFC 6749 section 4.1.2: a code is single use, and presenting it again revokes what it was redeemed for.
    [Test]
    public async Task Token_WhenCodeReused_ShouldReturnInvalidGrant()
    {
        var code = await _endpoint.CodeAsync(_clientId);
        var first = await TokenEndpoint.OkBodyAsync(await _endpoint.RedeemAsync(_clientId, code));

        var again = await _endpoint.RedeemAsync(_clientId, code);

        (await TokenEndpoint.ErrorAsync(again)).ShouldBe("invalid_grant");
        (await _endpoint.McpStatusAsync(first.GetProperty("access_token").GetString()!)).ShouldBe(HttpStatusCode.Unauthorized);
        (await TokenEndpoint.ErrorAsync(await _endpoint.RefreshAsync(_clientId, first.GetProperty("refresh_token").GetString()!)))
            .ShouldBe("invalid_grant");
    }

    // A replay of a code that never got tokens (its first redeem failed a binding) has nothing to revoke, and other
    // families are left alone.
    [Test]
    public async Task Token_WhenRefusedCodeReused_ShouldLeaveOtherTokensAlone()
    {
        var (access, _) = await _endpoint.TokensAsync(_clientId);
        var code = await _endpoint.CodeAsync(_clientId);
        await _endpoint.RedeemAsync(_clientId, code, ("code_verifier", new string('a', 43)));

        (await TokenEndpoint.ErrorAsync(await _endpoint.RedeemAsync(_clientId, code))).ShouldBe("invalid_grant");

        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_WhenSameCodeRedeemedInParallel_ShouldIssueOnce()
    {
        const int attempts = 20;
        var code = await _endpoint.CodeAsync(_clientId);
        using var start = new Barrier(attempts);

        var responses = await Task.WhenAll(Enumerable.Range(0, attempts).Select(_ => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait(TimeSpan.FromSeconds(30)).ShouldBeTrue("every attempt should reach the start line");
                return _endpoint.RedeemAsync(_clientId, code);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap()));

        var winner = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ShouldHaveSingleItem();
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            (await TokenEndpoint.ErrorAsync(refused)).ShouldBe("invalid_grant");
        }
        // Every loser found the code spent after the winner's spend and issue committed together, so each revoked what it
        // was spent for (RFC 6749 section 4.1.2), the winner's tokens included.
        var access = (await TokenEndpoint.OkBodyAsync(winner)).GetProperty("access_token").GetString()!;
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Refresh_ShouldRotate()
    {
        var (access, refresh) = await _endpoint.TokensAsync(_clientId);

        var body = await TokenEndpoint.OkBodyAsync(await _endpoint.RefreshAsync(_clientId, refresh));

        var rotatedAccess = body.GetProperty("access_token").GetString().ShouldNotBeNull();
        var rotatedRefresh = body.GetProperty("refresh_token").GetString().ShouldNotBeNull();
        rotatedAccess.ShouldNotBe(access);
        rotatedRefresh.ShouldNotBe(refresh);
        body.GetProperty("token_type").GetString().ShouldBe("Bearer");
        body.GetProperty("expires_in").GetInt32().ShouldBe(3600);
        body.GetProperty("scope").GetString().ShouldBe("decks");
        (await _endpoint.McpStatusAsync(rotatedAccess)).ShouldBe(HttpStatusCode.OK);
        await TokenEndpoint.OkBodyAsync(await _endpoint.RefreshAsync(_clientId, rotatedRefresh));
    }

    [Test]
    public async Task Refresh_WhenOldTokenReused_ShouldRevokeFamily()
    {
        var (_, refresh) = await _endpoint.TokensAsync(_clientId);
        var rotated = await TokenEndpoint.OkBodyAsync(await _endpoint.RefreshAsync(_clientId, refresh));

        var replay = await _endpoint.RefreshAsync(_clientId, refresh);

        (await TokenEndpoint.ErrorAsync(replay)).ShouldBe("invalid_grant");
        (await _endpoint.McpStatusAsync(rotated.GetProperty("access_token").GetString()!)).ShouldBe(HttpStatusCode.Unauthorized);
        (await TokenEndpoint.ErrorAsync(await _endpoint.RefreshAsync(_clientId, rotated.GetProperty("refresh_token").GetString()!)))
            .ShouldBe("invalid_grant");
    }

    // D4: another client's attempt neither spends the token nor revokes the family.
    [Test]
    public async Task Refresh_WhenOtherClient_ShouldReturnInvalidGrant()
    {
        var (access, refresh) = await _endpoint.TokensAsync(_clientId);

        var response = await _endpoint.RefreshAsync(_otherClientId, refresh);

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_grant");
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);
        await TokenEndpoint.OkBodyAsync(await _endpoint.RefreshAsync(_clientId, refresh));
    }

    [Test]
    public async Task Revoke_ShouldStopMcpAccess()
    {
        var (access, refresh) = await _endpoint.TokensAsync(_clientId);
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);

        var response = await _endpoint.RevokeAsync(access);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized);
        (await TokenEndpoint.ErrorAsync(await _endpoint.RefreshAsync(_clientId, refresh))).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Revoke_WhenGivenTheRefreshToken_ShouldEndTheFamily()
    {
        var (access, refresh) = await _endpoint.TokensAsync(_clientId);

        (await _endpoint.RevokeAsync(refresh)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    // RFC 7009 section 2.2: an invalid token still gets 200.
    [Test]
    public async Task Revoke_WhenUnknownToken_ShouldReturn200()
    {
        var response = await _endpoint.RevokeAsync("not-a-token");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Test]
    public async Task Revoke_WhenNoToken_ShouldReturnInvalidRequest()
    {
        var response = await _anonymous.PostAsync("/oauth/revoke", new FormUrlEncodedContent([]));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_request");
    }

    [Test]
    public async Task Mcp_WhenAccessTokenExpired_ShouldReturn401()
    {
        var (access, _) = await _endpoint.TokensAsync(_clientId);
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);

        Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Token_ResponseShouldBeNoStore()
    {
        var ok = await _endpoint.RedeemAsync(_clientId, await _endpoint.CodeAsync(_clientId));
        var refused = await _endpoint.RedeemAsync(_clientId, "not-a-code");

        foreach (var response in new[] { ok, refused })
        {
            response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
            response.Headers.Pragma.ToString().ShouldBe("no-cache");
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        }
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // An agent's real traffic (a redeem, refreshes about hourly, a revoke) is far below the budget: a whole minute's worth
    // of requests from one address all go through, token and revoke alike.
    [Test]
    public async Task Token_WhenUpToSixtyRequestsAMinuteFromOneAddress_ShouldServeThemAll()
    {
        var (access, refresh) = await _endpoint.TokensAsync(_clientId);
        var rotated = await TokenEndpoint.OkBodyAsync(await _endpoint.RefreshAsync(_clientId, refresh));
        for (var i = 2; i < 59; i++)
        {
            (await _endpoint.RevokeAsync("not-a-token")).StatusCode.ShouldBe(HttpStatusCode.OK, $"request {i + 1}");
        }

        (await _endpoint.RevokeAsync(rotated.GetProperty("refresh_token").GetString()!)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    // POST /oauth/token and /oauth/revoke share one budget per address; past it, both get 429 with Retry-After and an
    // RFC 6749 error body (still no-store) until the window passes.
    [Test]
    public async Task Token_WhenOverSixtyAMinuteFromOneAddress_ShouldReturn429UntilTheWindowPasses()
    {
        for (var i = 0; i < 60; i++)
        {
            (await _endpoint.RevokeAsync("not-a-token")).StatusCode.ShouldBe(HttpStatusCode.OK, $"request {i + 1}");
        }
        var code = await _endpoint.CodeAsync(_clientId);

        var refusedToken = await _endpoint.RedeemAsync(_clientId, code);
        var refusedRevoke = await _endpoint.RevokeAsync("not-a-token");

        foreach (var refused in new[] { refusedToken, refusedRevoke })
        {
            (await TokenEndpoint.ErrorAsync(refused, HttpStatusCode.TooManyRequests)).ShouldBe("too_many_requests");
            refused.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldBe(TimeSpan.FromMinutes(1));
            refused.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        }
        Clock.Advance(TimeSpan.FromMinutes(1));
        await TokenEndpoint.OkBodyAsync(await _endpoint.RedeemAsync(_clientId, code));
    }

    // RFC 6749 section 3.2: no parameter appears twice, so a second value can't be smuggled past a check that read the first.
    [TestCase("code")]
    [TestCase("client_id")]
    [TestCase("code_verifier")]
    [TestCase("grant_type")]
    public async Task Token_WhenParameterRepeated_ShouldReturnInvalidRequest(string parameter)
    {
        var code = await _endpoint.CodeAsync(_clientId);
        var fields = RedeemFields(code);
        fields.Add(KeyValuePair.Create(parameter, fields.First(f => f.Key == parameter).Value));

        var response = await _anonymous.PostAsync("/oauth/token", new FormUrlEncodedContent(fields));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_request");
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        await TokenEndpoint.OkBodyAsync(await _endpoint.RedeemAsync(_clientId, code));
    }

    [Test]
    public async Task Revoke_WhenTokenRepeated_ShouldReturnInvalidRequest()
    {
        var (access, _) = await _endpoint.TokensAsync(_clientId);

        var response = await _anonymous.PostAsync("/oauth/revoke", new FormUrlEncodedContent([KeyValuePair.Create("token", "not-a-token"), KeyValuePair.Create("token", access)]));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_request");
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);
    }

    // Only application/x-www-form-urlencoded (RFC 6749 section 4.1.3); the code isn't spent by a refused body.
    [Test]
    public async Task Token_WhenMultipartFormData_ShouldReturnInvalidRequest()
    {
        var code = await _endpoint.CodeAsync(_clientId);
        using var multipart = new MultipartFormDataContent();
        foreach (var (name, value) in RedeemFields(code))
        {
            multipart.Add(new StringContent(value), name);
        }

        var response = await _anonymous.PostAsync("/oauth/token", multipart);

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_request");
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        await TokenEndpoint.OkBodyAsync(await _endpoint.RedeemAsync(_clientId, code));
    }

    [Test]
    public async Task Token_WhenJson_ShouldReturnInvalidRequest()
    {
        var code = await _endpoint.CodeAsync(_clientId);

        var response = await _anonymous.PostAsJsonAsync("/oauth/token", RedeemFields(code).ToDictionary(f => f.Key, f => f.Value));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_request");
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        await TokenEndpoint.OkBodyAsync(await _endpoint.RedeemAsync(_clientId, code));
    }

    [Test]
    public async Task Revoke_WhenMultipartFormData_ShouldReturnInvalidRequest()
    {
        var (access, _) = await _endpoint.TokensAsync(_clientId);
        using var multipart = new MultipartFormDataContent { { new StringContent(access), "token" } };

        var response = await _anonymous.PostAsync("/oauth/revoke", multipart);

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_request");
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);
    }

    // A form over the framework's limits (here 1,025 values, one more than it reads) is the client's mistake: 400
    // invalid_request, still no-store, not a 500. The code isn't spent.
    [Test]
    public async Task Token_WhenFormOverTheFrameworkLimits_ShouldReturnInvalidRequest()
    {
        var code = await _endpoint.CodeAsync(_clientId);
        var fields = RedeemFields(code);
        fields.AddRange(Enumerable.Range(0, 1_025 - fields.Count).Select(i => KeyValuePair.Create($"extra{i}", "x")));

        var response = await _anonymous.PostAsync("/oauth/token", new FormUrlEncodedContent(fields));

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_request");
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        await TokenEndpoint.OkBodyAsync(await _endpoint.RedeemAsync(_clientId, code));
    }

    // A charset parameter on the right media type is fine.
    [Test]
    public async Task Token_WhenFormUrlEncodedWithCharset_ShouldReturnTokens()
    {
        var code = await _endpoint.CodeAsync(_clientId);
        using var form = new FormUrlEncodedContent(RedeemFields(code));
        form.Headers.ContentType!.CharSet = "utf-8";

        var body = await TokenEndpoint.OkBodyAsync(await _anonymous.PostAsync("/oauth/token", form));

        body.GetProperty("token_type").GetString().ShouldBe("Bearer");
    }

    private List<KeyValuePair<string, string>> RedeemFields(string code) =>
    [
        KeyValuePair.Create("grant_type", "authorization_code"),
        KeyValuePair.Create("code", code),
        KeyValuePair.Create("client_id", _clientId),
        KeyValuePair.Create("redirect_uri", OAuthFlow.REDIRECT),
        KeyValuePair.Create("code_verifier", OAuthFlow.VERIFIER),
        KeyValuePair.Create("resource", OAuthFlow.Resource),
    ];

    // A code issued just before an admin revokes its presenter is spent for nothing: no tokens, so approving the person
    // again can't bring any back.
    [Test]
    public async Task Token_WhenPresenterRevokedAfterCodeIssued_ShouldIssueNoTokens()
    {
        var presenter = TestUser.NewPending();
        using (var signedIn = ClientFor(presenter))
        {
            (await signedIn.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        using var ada = ClientFor(TestUser.Ada);
        var id = await AdminUserIdAsync(ada, presenter.Email);
        (await ada.PostAsync($"/api/admin/users/{id}/approve", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var code = await _endpoint.CodeAsync(_clientId, presenter);
        (await ada.PostAsync($"/api/admin/users/{id}/revoke", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _endpoint.RedeemAsync(_clientId, code);

        (await TokenEndpoint.ErrorAsync(response)).ShouldBe("invalid_grant");
        (await ada.PostAsync($"/api/admin/users/{id}/approve", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        // The library still offers to connect an agent, which it does only while the person has no live agent tokens:
        // none were stored for the refused redeem, so approving them again revived nothing. Checked before the replay
        // below, which would revoke anything issued for the code.
        using var approvedAgain = ClientFor(presenter);
        var library = await TokenEndpoint.OkBodyAsync(await approvedAgain.GetAsync("/api/decks"));
        library.GetProperty("connect").GetProperty("show").GetBoolean().ShouldBeTrue();
        (await TokenEndpoint.ErrorAsync(await _endpoint.RedeemAsync(_clientId, code))).ShouldBe("invalid_grant");
    }

    private static async Task<Guid> AdminUserIdAsync(HttpClient admin, string email)
    {
        var view = await admin.GetFromJsonAsync<JsonElement>("/api/admin/users");
        return view.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetGuid();
    }

    private async Task<AgentPrincipal?> ValidateAsync(string accessToken)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAgentTokens>().ValidateAsync(accessToken, OAuthFlow.Resource);
    }
}
