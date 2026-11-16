using System.Net;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity;

// A browser signed in through TestAuthScheme's sw-test-user cookie, as the acceptance tests sign one in, can sign out:
// POST /api/signout passes the guard and the SignedIn policy, and the cookie is gone afterwards, so /api/me is 401.
[TestFixture]
public class TestSignOutTests : ApiTestFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    public async Task SignOut_WhenSignedInByTestCookie_ShouldEndSession()
    {
        var jar = new CookieContainer();
        var baseAddress = new Uri(TestAppFactory.PUBLIC_URL);
        jar.Add(baseAddress, new Cookie(TestAuthScheme.COOKIE, TestUser.Sam.Email));
        using var browser = Factory.CreateDefaultClient(baseAddress, new CookieContainerHandler(jar));
        browser.DefaultRequestHeaders.Add("Origin", TestAppFactory.PUBLIC_URL);
        using var before = await browser.GetAsync("/api/me");
        before.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await browser.PostAsync("/api/signout", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        jar.GetCookies(baseAddress)[TestAuthScheme.COOKIE].ShouldBeNull();
        using var after = await browser.GetAsync("/api/me");
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
