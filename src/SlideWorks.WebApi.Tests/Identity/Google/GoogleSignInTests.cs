using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Infrastructure.Identity.Google;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Google;

// Sign-in with Google end to end against TestIdentityProvider: the person lands where they were going with a session,
// and every refusal lands on /signin?error=<code> with no session and nothing changed.
[TestFixture]
public class GoogleSignInTests : GoogleSignInFixture
{
    [Test]
    public async Task SignIn_WhenProviderReturnsVerifiedEmail_ShouldCreateUserAndSession()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-new", "New.Person@Example.test", "New Person", picture: "https://images.example/p.png");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/decks");
        var me = await MeAsync(browser);
        me.GetProperty("email").GetString().ShouldBe("new.person@example.test");
        me.GetProperty("name").GetString().ShouldBe("New Person");
        me.GetProperty("pictureUrl").GetString().ShouldBe("https://images.example/p.png");
    }

    // A first sign-in asks for access: the person is stored Pending and still gets a session, so the app can show them
    // the waiting page and let them sign out.
    [Test]
    public async Task SignIn_WhenNewEmail_ShouldCreatePendingUser()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-pending", "pending.person@example.test");

        response.Headers.Location!.OriginalString.ShouldBe("/decks");
        var me = await MeAsync(browser);
        me.GetProperty("access").GetString().ShouldBe("pending");
        me.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
        using var decks = await browser.GetAsync("/api/decks");
        decks.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await decks.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("pending_approval");
    }

    // The test hosts set SLIDEWORKS_ADMIN_EMAIL to Ada's email, and nobody in this fixture is admin yet.
    [Test]
    public async Task SignIn_WhenAdminEmailAndNoAdmin_ShouldCreateApprovedAdmin()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-first-admin", TestUser.Ada.Email.ToUpperInvariant(), TestUser.Ada.Name);

        response.Headers.Location!.OriginalString.ShouldBe("/decks");
        var me = await MeAsync(browser);
        me.GetProperty("access").GetString().ShouldBe("approved");
        me.GetProperty("isAdmin").GetBoolean().ShouldBeTrue();
        using var admin = await browser.GetAsync("/api/admin/users");
        admin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task SignIn_WhenSessionIssued_ShouldBeTheHttpOnlyLaxSessionCookie()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-cookie", "cookie@example.test");

        SessionCookies.Attributes(response).ShouldContain("httponly");
        SessionCookies.Attributes(response).ShouldContain("samesite=lax");
    }

    [Test]
    public async Task SignIn_WhenEmailUnverified_ShouldRefuse()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-unverified", "unverified@example.test", emailVerified: false);

        response.Headers.Location!.OriginalString.ShouldBe("/signin?error=unverified&returnTo=%2Fdecks");
        await ShouldHaveNoSessionAsync(browser);
        // Nobody was created: the email is still free for another Google account.
        using var other = Browser();
        using var otherSignIn = await SignInAsync(other, "sub-verified-later", "unverified@example.test");
        otherSignIn.Headers.Location!.OriginalString.ShouldBe("/decks");
    }

    [Test]
    public async Task SignIn_WhenEmailOwnedByOtherSubject_ShouldRefuse()
    {
        using var owner = Browser();
        using var ownerSignIn = await SignInAsync(owner, "sub-owner", "taken@example.test", "First Owner");
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-intruder", "Taken@example.test", "Someone Else");

        response.Headers.Location!.OriginalString.ShouldBe("/signin?error=email_linked&returnTo=%2Fdecks");
        await ShouldHaveNoSessionAsync(browser);
        (await MeAsync(owner)).GetProperty("name").GetString().ShouldBe("First Owner");
    }

    // Two Google accounts with one email sign in for the first time at the same moment: both find the email free, and the
    // database's unique email decides. One gets in, the other is told the email is linked, and neither sees a 500. Several
    // rounds, each with its exchanges held until both arrive, so the race is hit rather than hoped for.
    [Test]
    public async Task SignIn_WhenTwoSubjectsShareANewEmailAtOnce_ShouldLetOneInAndRefuseTheOther()
    {
        for (var round = 0; round < 5; round++)
        {
            var email = $"race-{round}-{Guid.NewGuid():N}@example.test";
            using var first = Browser();
            using var second = Browser();
            var firstStart = await StartAsync(first);
            var secondStart = await StartAsync(second);
            Provider.AnswerCode($"code-a-{round}", Provider.Mint(CLIENT_ID, Clock.GetUtcNow(), $"sub-race-a-{round}", email, "A", true, firstStart["nonce"].ToString()));
            Provider.AnswerCode($"code-b-{round}", Provider.Mint(CLIENT_ID, Clock.GetUtcNow(), $"sub-race-b-{round}", email, "B", true, secondStart["nonce"].ToString()));
            Provider.HoldExchangesUntil(2);
            HttpResponseMessage[] responses;
            try
            {
                responses = await Task.WhenAll(
                    first.GetAsync($"{GoogleSignInSetup.CALLBACK_PATH}?code=code-a-{round}&state={Uri.EscapeDataString(firstStart["state"].ToString())}"),
                    second.GetAsync($"{GoogleSignInSetup.CALLBACK_PATH}?code=code-b-{round}&state={Uri.EscapeDataString(secondStart["state"].ToString())}"));
            }
            finally
            {
                Provider.ReleaseExchanges();
            }

            var locations = responses.Select(r => r.Headers.Location?.OriginalString).ToList();
            responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Redirect);
            locations.ShouldBe(["/decks", "/signin?error=email_linked&returnTo=%2Fdecks"], ignoreOrder: true);
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Test]
    public async Task SignIn_WhenSameSubjectAgain_ShouldUpdateNotDuplicate()
    {
        using var first = Browser();
        using var firstSignIn = await SignInAsync(first, "sub-moves", "before@example.test", "Before Name");
        using var second = Browser();

        using var secondSignIn = await SignInAsync(second, "sub-moves", "after@example.test", "After Name");

        secondSignIn.Headers.Location!.OriginalString.ShouldBe("/decks");
        var me = await MeAsync(second);
        me.GetProperty("email").GetString().ShouldBe("after@example.test");
        me.GetProperty("name").GetString().ShouldBe("After Name");
        // The old email moved with the person rather than staying on a second row, so another account can take it.
        using var third = Browser();
        using var thirdSignIn = await SignInAsync(third, "sub-takes-old-email", "before@example.test");
        thirdSignIn.Headers.Location!.OriginalString.ShouldBe("/decks");
    }

    // Each value is sent raw in the query string, so the app sees it URL-decoded once.
    [TestCase("https%3A%2F%2Fevil.example%2Fdecks")]
    [TestCase("http:evil.example")]
    [TestCase("evil.example")]
    [TestCase("//evil.example")]
    [TestCase("%2F%2Fevil.example")]
    [TestCase("/%5Cevil.example")]
    [TestCase("%2F%5Cevil.example")]
    [TestCase("%5C%5Cevil.example")]
    [TestCase("%252F%252Fevil.example")]
    [TestCase("/%09/evil.example")]
    [TestCase("")]
    public async Task SignIn_WhenReturnToExternal_ShouldRedirectHome(string rawReturnTo)
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, $"sub-external-{Guid.NewGuid():N}", $"external-{Guid.NewGuid():N}@example.test", rawReturnTo: rawReturnTo);

        response.Headers.Location!.OriginalString.ShouldBe("/");
        await MeAsync(browser);
    }

    // Kestrel refuses a non-ASCII header value, so a Location carrying one would be a 500 after sign-in.
    [TestCase("%2Fd%C3%A9cks")]
    [TestCase("%2Fdecks%E2%80%8B")]
    [TestCase("%2Fdecks%7F")]
    public async Task SignIn_WhenReturnToHasCharacterAboveTilde_ShouldRedirectHome(string rawReturnTo)
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, $"sub-non-ascii-{Guid.NewGuid():N}", $"non-ascii-{Guid.NewGuid():N}@example.test", rawReturnTo: rawReturnTo);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/");
        await MeAsync(browser);
    }

    [Test]
    public async Task SignIn_WhenReturnToLocalWithQuery_ShouldKeepItIntact()
    {
        const string returnTo = "/oauth/authorize?client_id=abc&redirect_uri=http%3A%2F%2F127.0.0.1%3A9%2Fcb&state=x%2By&code_challenge=c#top";
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-local", "local@example.test", rawReturnTo: Uri.EscapeDataString(returnTo));

        response.Headers.Location!.OriginalString.ShouldBe(returnTo);
    }

    [Test]
    public async Task Start_WhenCalled_ShouldUseStateNoncePkceAndThePublicCallback()
    {
        using var browser = Browser();

        var query = await StartAsync(browser);

        query["client_id"].ToString().ShouldBe(CLIENT_ID);
        query["response_type"].ToString().ShouldBe("code");
        query["redirect_uri"].ToString().ShouldBe($"{TestAppFactory.PUBLIC_URL}{GoogleSignInSetup.CALLBACK_PATH}");
        query["scope"].ToString().Split(' ').ShouldBe(["openid", "profile", "email"], ignoreOrder: true);
        query["code_challenge_method"].ToString().ShouldBe("S256");
        query["code_challenge"].ToString().ShouldNotBeNullOrEmpty();
        query["state"].ToString().ShouldNotBeNullOrEmpty();
        query["nonce"].ToString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task SignIn_WhenExchangingTheCode_ShouldSendSecretVerifierAndTheSameCallback()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-exchange", "exchange@example.test");

        Provider.LastTokenRequest!["client_secret"].ShouldBe(CLIENT_SECRET);
        Provider.LastTokenRequest["code_verifier"].ShouldNotBeNullOrEmpty();
        Provider.LastTokenRequest["redirect_uri"].ShouldBe($"{TestAppFactory.PUBLIC_URL}{GoogleSignInSetup.CALLBACK_PATH}");
    }

    [Test]
    public async Task SignIn_WhenNonceDoesNotMatch_ShouldRefuse()
    {
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-replay", "replay@example.test", nonce: "a-nonce-from-another-attempt");

        response.Headers.Location!.OriginalString.ShouldBe("/signin?error=failed&returnTo=%2Fdecks");
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task Callback_WhenAccessDenied_ShouldShowError()
    {
        using var browser = Browser();
        var query = await StartAsync(browser, "%2Fdecks%2Fabc");

        using var response = await browser.GetAsync($"{GoogleSignInSetup.CALLBACK_PATH}?error=access_denied&state={Uri.EscapeDataString(query["state"].ToString())}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/signin?error=cancelled&returnTo=%2Fdecks%2Fabc");
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task Callback_WhenStateTampered_ShouldRefuse()
    {
        using var browser = Browser();
        var query = await StartAsync(browser);
        Provider.NextIdToken = Provider.Mint(CLIENT_ID, Clock.GetUtcNow(), "sub-tampered", "tampered@example.test", "T", true, query["nonce"].ToString());

        using var response = await browser.GetAsync($"{GoogleSignInSetup.CALLBACK_PATH}?code=test-code&state={Uri.EscapeDataString(query["state"].ToString() + "x")}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldStartWith("/signin?error=failed");
        await ShouldHaveNoSessionAsync(browser);
    }

    // Another browser's state (login CSRF): the state is genuine, but this browser has no correlation cookie for it.
    [Test]
    public async Task Callback_WhenStateFromAnotherBrowser_ShouldRefuse()
    {
        using var attacker = Browser();
        var query = await StartAsync(attacker);
        Provider.NextIdToken = Provider.Mint(CLIENT_ID, Clock.GetUtcNow(), "sub-attacker", "attacker@example.test", "A", true, query["nonce"].ToString());
        using var victim = Browser();

        using var response = await victim.GetAsync($"{GoogleSignInSetup.CALLBACK_PATH}?code=test-code&state={Uri.EscapeDataString(query["state"].ToString())}");

        response.Headers.Location!.OriginalString.ShouldStartWith("/signin?error=failed");
        await ShouldHaveNoSessionAsync(victim);
    }

    [Test]
    public async Task Callback_WhenPosted_ShouldRefuse()
    {
        using var browser = Browser();
        var query = await StartAsync(browser);
        Provider.NextIdToken = Provider.Mint(CLIENT_ID, Clock.GetUtcNow(), "sub-posted", "posted@example.test", "P", true, query["nonce"].ToString());

        using var response = await browser.PostAsync(
            GoogleSignInSetup.CALLBACK_PATH,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "test-code", ["state"] = query["state"].ToString() }));

        response.Headers.Location!.OriginalString.ShouldStartWith("/signin?error=failed");
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task Options_WhenConfigured_ShouldOfferGoogle()
    {
        using var browser = Browser();

        var options = JsonDocument.Parse(await browser.GetStringAsync("/api/signin/options")).RootElement;

        options.GetProperty("google").GetBoolean().ShouldBeTrue();
    }
}
