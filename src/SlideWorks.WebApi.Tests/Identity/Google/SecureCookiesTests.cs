using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using SlideWorks.WebApi.Infrastructure.Identity.Google;

namespace SlideWorks.WebApi.Tests.Identity.Google;

// Behind a TLS proxy the app serves plain HTTP, and an https SLIDEWORKS_PUBLIC_URL is what makes it act as https: Google
// is told the https callback, and the correlation, nonce and session cookies are all Secure.
[TestFixture]
public class SecureCookiesTests : GoogleSignInFixture
{
    private const string PUBLIC_URL = "https://slides.example";

    protected override Dictionary<string, string?> ExtraConfig => new(base.ExtraConfig) { ["SLIDEWORKS_PUBLIC_URL"] = PUBLIC_URL };

    [Test]
    public async Task SignIn_WhenPublicUrlIsHttps_ShouldUseHttpsCallbackAndSecureCookies()
    {
        // The cookie jar only sends Secure cookies back over https, as a real browser would.
        using var browser = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

        using var start = await browser.GetAsync("/signin/google?returnTo=%2Fdecks");

        start.StatusCode.ShouldBe(HttpStatusCode.Found);
        var query = QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        query["redirect_uri"].ToString().ShouldBe($"{PUBLIC_URL}{GoogleSignInSetup.CALLBACK_PATH}");
        AttributesOf(start, ".AspNetCore.Correlation.").ShouldContain("secure");
        AttributesOf(start, ".AspNetCore.OpenIdConnect.Nonce.").ShouldContain("secure");

        Provider.NextIdToken = Provider.Mint(CLIENT_ID, Clock.GetUtcNow(), "sub-secure", "secure@example.test", "S", true, query["nonce"].ToString());
        using var callback = await browser.GetAsync($"{GoogleSignInSetup.CALLBACK_PATH}?code=test-code&state={Uri.EscapeDataString(query["state"].ToString())}");

        callback.Headers.Location!.OriginalString.ShouldBe("/decks");
        Provider.LastTokenRequest!["redirect_uri"].ShouldBe($"{PUBLIC_URL}{GoogleSignInSetup.CALLBACK_PATH}");
        SessionCookies.Attributes(callback).ShouldContain("secure");
    }

    // The attributes of the one Set-Cookie whose name starts with namePrefix, lower-cased.
    private static IReadOnlyList<string> AttributesOf(HttpResponseMessage response, string namePrefix)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(namePrefix, StringComparison.Ordinal));
        return header.Split(';').Skip(1).Select(part => part.Trim().ToLowerInvariant()).ToArray();
    }
}
