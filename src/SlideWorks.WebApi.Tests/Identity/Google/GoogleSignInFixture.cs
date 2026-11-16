using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using SlideWorks.WebApi.Infrastructure.Identity.Google;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Google;

// An app with sign-in with Google set up, whose "google-sign-in" client reaches a TestIdentityProvider instead of
// Google. Each test signs in through a browser of its own: an anonymous client with its own cookie jar, which goes to
// /signin/google, "comes back" from Google to /signin-google with the state the app gave it, and keeps the session cookie.
public abstract class GoogleSignInFixture : ApiTestFixture
{
    protected const string CLIENT_ID = "test-client.apps.example";
    protected const string CLIENT_SECRET = "test-client-secret";

    protected TestIdentityProvider Provider { get; } = new();

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Dictionary<string, string?> ExtraConfig => new()
    {
        [GoogleSignInSettings.CLIENT_ID_KEY] = CLIENT_ID,
        [GoogleSignInSettings.CLIENT_SECRET_KEY] = CLIENT_SECRET,
    };

    protected override Dictionary<string, HttpMessageHandler> NamedHandlers => new()
    {
        [GoogleSignInSetup.HTTP_CLIENT_NAME] = Provider,
    };

    [OneTimeTearDown]
    public void DisposeProvider() => Provider.Dispose();

    protected HttpClient Browser() => ClientFor(TestUser.Anonymous);

    // GET /signin/google, which must send the browser to Google. Returns the query of that redirect.
    protected static async Task<Dictionary<string, StringValues>> StartAsync(HttpClient browser, string rawReturnTo = "%2Fdecks")
    {
        using var start = await browser.GetAsync($"/signin/google?returnTo={rawReturnTo}");
        start.StatusCode.ShouldBe(HttpStatusCode.Found);
        start.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe(TestIdentityProvider.AUTHORIZATION_ENDPOINT);
        return QueryHelpers.ParseQuery(start.Headers.Location.Query);
    }

    // The whole trip: start, Google answers the code exchange with an ID token for this person (carrying this attempt's
    // nonce unless a test says otherwise), and the browser returns to the callback. Returns the callback's response.
    protected async Task<HttpResponseMessage> SignInAsync(
        HttpClient browser,
        string subject,
        string email,
        string? name = "Test Person",
        bool emailVerified = true,
        string rawReturnTo = "%2Fdecks",
        string? nonce = null,
        string? picture = null)
    {
        var query = await StartAsync(browser, rawReturnTo);
        Provider.NextIdToken = Provider.Mint(CLIENT_ID, Clock.GetUtcNow(), subject, email, name, emailVerified, nonce ?? query["nonce"].ToString(), picture);
        return await browser.GetAsync($"{GoogleSignInSetup.CALLBACK_PATH}?code=test-code&state={Uri.EscapeDataString(query["state"].ToString())}");
    }

    protected static async Task<JsonElement> MeAsync(HttpClient browser)
    {
        using var me = await browser.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await me.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    protected static async Task ShouldHaveNoSessionAsync(HttpClient browser)
    {
        using var me = await browser.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
