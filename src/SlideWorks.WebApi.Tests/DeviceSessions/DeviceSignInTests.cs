using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Tests.RateLimits;
using SlideWorks.WebApi.Tests.Support;
using DeviceSessionStore = SlideWorks.WebApi.Features.DeviceSessions.DeviceSessions;

namespace SlideWorks.WebApi.Tests.DeviceSessions;

// A phone's sign-in (spec §3, K4): the confirm page mints a code from the cookie session, and the app exchanges it with its
// PKCE verifier for a device secret. Every exchange but the rate-limit test comes from its own remote address, so the
// exchange budget one caller gets never leaks between tests.
[TestFixture]
public class DeviceSignInTests : ApiTestFixture
{
    private const string SIGN_IN_EXPIRED = """{"error":"sign_in_expired","message":"Sign-in expired. Try again."}""";
    private const string STATE = "state-1234567890abcdef";
    private const string DEVICE = "Sam's Pixel";

    private CapturingLoggerProvider _logs = null!;
    private int _nextAddress;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        _logs = CapturingLoggerProvider.Replace<DeviceSessionStore>(services);
        TestRemoteAddress.AddTo(services);
    };

    [Test]
    public async Task Codes_WhenSignedIn_ShouldReturnOpenAppUrlWithStateAndIss()
    {
        using var response = await MintAsync(Client, Pkce.S256(Pkce.NewVerifier()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var url = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("openAppUrl").GetString().ShouldNotBeNull();
        url.ShouldMatch($"^slideworks://signed-in\\?code=[A-Za-z0-9_-]{{43}}&state={STATE}&iss={Uri.EscapeDataString(PublicUrl)}$");
    }

    [Test]
    public async Task Codes_WhenSignedOut_ShouldReturn401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await MintAsync(anonymous, Pkce.S256(Pkce.NewVerifier()));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Codes_WhenPending_ShouldMint()
    {
        using var pending = ClientFor(TestUser.NewPending());

        using var response = await MintAsync(pending, Pkce.S256(Pkce.NewVerifier()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("openAppUrl").GetString().ShouldNotBeNull().ShouldStartWith("slideworks://signed-in?code=", Case.Sensitive);
    }

    // D9: the setting is read on every mint, so flipping it on the running server takes effect at once.
    [Test]
    public async Task Codes_WhenBehindIdentityProxy_ShouldRefuseWithIdentityProxyAndMessage()
    {
        var configuration = Factory.Services.GetRequiredService<IConfiguration>();
        configuration[DeviceSignIn.IDENTITY_PROXY_SETTING] = "true";
        HttpResponseMessage response;
        try
        {
            response = await MintAsync(Client, Pkce.S256(Pkce.NewVerifier()));
        }
        finally
        {
            configuration[DeviceSignIn.IDENTITY_PROXY_SETTING] = "false";
        }

        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).ShouldBe(
                """{"error":"identity_proxy","message":"The SlideWorks app can't sign in to this server, because it sits behind an identity proxy."}""");
        }
    }

    [Test]
    public async Task Codes_WhenChallengeMalformed_ShouldRefuseWithInvalidRequest()
    {
        using var response = await MintAsync(Client, "not+base64url/and-too-short");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"invalid_request","message":"challenge must be 43 base64url characters."}""");
    }

    [Test]
    public async Task Codes_WhenDeviceNameTooLong_ShouldRefuseWithInvalidRequest()
    {
        using var response = await MintAsync(Client, Pkce.S256(Pkce.NewVerifier()), new string('x', 81));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"invalid_request","message":"deviceName must be 1 to 80 characters."}""");
    }

    [Test]
    public async Task Exchange_WhenVerifierMatches_ShouldReturnSessionSecretAndMe()
    {
        var verifier = Pkce.NewVerifier();
        var code = await MintCodeAsync(Client, verifier);

        using var response = await ExchangeAsync(code, verifier);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("secret").GetString().ShouldNotBeNull().ShouldMatch("^swd_[A-Za-z0-9_-]{43}$");
        body.GetProperty("session").GetProperty("current").GetBoolean().ShouldBeTrue();
        body.GetProperty("session").GetProperty("signedInLabel").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("me").GetProperty("email").GetString().ShouldBe(TestUser.Sam.Email);
        body.GetProperty("me").GetProperty("access").GetString().ShouldBe("approved");
    }

    [Test]
    public async Task Exchange_WhenVerifierMatches_ShouldNameTheSessionAfterTheDevice()
    {
        var verifier = Pkce.NewVerifier();
        var code = await MintCodeAsync(Client, verifier, $"  {DEVICE}  ");

        using var response = await ExchangeAsync(code, verifier);

        var session = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("session");
        session.GetProperty("deviceName").GetString().ShouldBe(DEVICE);
        var listed = await Client.GetFromJsonAsync<JsonElement>("/api/auth/device-sessions");
        listed.GetProperty("devices").EnumerateArray()
            .Single(d => d.GetProperty("id").GetString() == session.GetProperty("id").GetString())
            .GetProperty("deviceName").GetString().ShouldBe(DEVICE);
    }

    [Test]
    public async Task Exchange_WhenVerifierWrong_ShouldRefuseAndBurnTheCode()
    {
        var verifier = Pkce.NewVerifier();
        var code = await MintCodeAsync(Client, verifier);

        using var wrong = await ExchangeAsync(code, Pkce.NewVerifier());
        using var right = await ExchangeAsync(code, verifier);

        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await wrong.Content.ReadAsStringAsync()).ShouldBe(SIGN_IN_EXPIRED);
        right.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await right.Content.ReadAsStringAsync()).ShouldBe(SIGN_IN_EXPIRED);
    }

    [Test]
    public async Task Exchange_WhenCodeReused_ShouldRefuse()
    {
        var verifier = Pkce.NewVerifier();
        var code = await MintCodeAsync(Client, verifier);

        using var first = await ExchangeAsync(code, verifier);
        using var second = await ExchangeAsync(code, verifier);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).ShouldBe(SIGN_IN_EXPIRED);
    }

    [Test]
    public async Task Exchange_WhenCodeOlderThanTwoMinutes_ShouldRefuse()
    {
        var verifier = Pkce.NewVerifier();
        var code = await MintCodeAsync(Client, verifier);
        Clock.Advance(DeviceSessionStore.CODE_LIFETIME + TimeSpan.FromSeconds(1));

        using var response = await ExchangeAsync(code, verifier);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe(SIGN_IN_EXPIRED);
    }

    // The person was pending at mint and an admin approved them before the exchange: their access stamp moved, so the code
    // no longer speaks for them.
    [Test]
    public async Task Exchange_WhenAccessChangedSinceMint_ShouldRefuse()
    {
        var person = TestUser.NewPending();
        using var pending = ClientFor(person);
        var verifier = Pkce.NewVerifier();
        var code = await MintCodeAsync(pending, verifier);
        using var admin = ClientFor(TestUser.Ada);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var user = await TestPeople.SignInAsync(scope.ServiceProvider, person);
            (await admin.PostAsync($"/api/admin/users/{user.Id}/approve", null)).EnsureSuccessStatusCode();
        }

        using var response = await ExchangeAsync(code, verifier);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe(SIGN_IN_EXPIRED);
    }

    [Test]
    public async Task Exchange_WhenSucceeds_ShouldSendNoStore()
    {
        var verifier = Pkce.NewVerifier();
        var code = await MintCodeAsync(Client, verifier);

        using var response = await ExchangeAsync(code, verifier);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
    }

    [Test]
    public async Task Exchange_WhenTooManyAttempts_ShouldReturn429()
    {
        const string caller = "203.0.113.200";
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < ExchangeLimiter.PER_MINUTE; i++)
        {
            using var attempt = await ExchangeAsync("unknown-code", Pkce.NewVerifier(), caller);
            statuses.Add(attempt.StatusCode);
        }

        using var over = await ExchangeAsync("unknown-code", Pkce.NewVerifier(), caller);

        statuses.ShouldAllBe(s => s == HttpStatusCode.BadRequest);
        over.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await over.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_attempts"}""");
        over.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Test]
    public async Task Exchange_WhenSucceeds_ShouldLogCreationWithoutTheSecret()
    {
        var verifier = Pkce.NewVerifier();
        var code = await MintCodeAsync(Client, verifier);

        using var response = await ExchangeAsync(code, verifier);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("session").GetProperty("id").GetString().ShouldNotBeNull();
        var secret = body.GetProperty("secret").GetString().ShouldNotBeNull();
        _logs.Lines.ShouldContain(line => line.Contains($"Device session {id} created for ", StringComparison.Ordinal));
        _logs.Lines.ShouldNotContain(line => line.Contains(secret, StringComparison.Ordinal) || line.Contains(code, StringComparison.Ordinal));
    }

    private static Task<HttpResponseMessage> MintAsync(HttpClient client, string challenge, string deviceName = DEVICE)
        => client.PostAsJsonAsync("/api/auth/device-sessions/codes", new { challenge, state = STATE, deviceName });

    private static async Task<string> MintCodeAsync(HttpClient client, string verifier, string deviceName = DEVICE)
    {
        using var response = await MintAsync(client, Pkce.S256(verifier), deviceName);
        response.EnsureSuccessStatusCode();
        var url = new Uri((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("openAppUrl").GetString().ShouldNotBeNull());
        return System.Web.HttpUtility.ParseQueryString(url.Query)["code"].ShouldNotBeNull();
    }

    // An anonymous exchange as the app makes it: no cookie and no Origin, from its own remote address unless one is given.
    private async Task<HttpResponseMessage> ExchangeAsync(string code, string verifier, string? remote = null)
    {
        using var anonymous = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/device-sessions/exchange")
        {
            Content = JsonContent.Create(new { code, verifier }),
        };
        request.Headers.Add(TestRemoteAddress.HEADER, remote ?? $"198.51.100.{Interlocked.Increment(ref _nextAddress)}");
        return await anonymous.SendAsync(request);
    }
}
