using System.Net;

namespace SlideWorks.WebApi.Tests.Identity.Google;

// POST /api/signout ends a real session cookie, and like every write under /api it refuses another site.
[TestFixture]
public class SignOutTests : GoogleSignInFixture
{
    [Test]
    public async Task SignOut_ShouldEndSession()
    {
        using var browser = Browser();
        using var signIn = await SignInAsync(browser, "sub-signs-out", "signs.out@example.test");
        await MeAsync(browser);

        using var response = await browser.PostAsync("/api/signout", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldHaveNoSessionAsync(browser);
    }

    [Test]
    public async Task SignOut_WhenFromAnotherSite_ShouldBeRefusedAndKeepSession()
    {
        using var browser = Browser();
        using var signIn = await SignInAsync(browser, "sub-stays", "stays@example.test");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/signout");
        request.Headers.Remove("Origin");
        request.Headers.Add("Origin", "https://evil.example");
        browser.DefaultRequestHeaders.Remove("Origin");

        using var response = await browser.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await MeAsync(browser);
    }
}
