using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity;

// People are signed in by a cookie session (K1). Signed out, the API answers 401 JSON and never redirects; signed in,
// GET /api/me names the person.
[TestFixture]
public class CookieAuthTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => SessionProbeController.AddTo;

    [Test]
    public async Task Api_WhenSignedOut_ShouldReturn401Json()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Not signed in");
    }

    [Test]
    public async Task Me_WhenSignedIn_ShouldReturnPresenter()
    {
        var response = await Client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(body, "me.json", "pictureUrl", "avatar.pictureUrl", "timeZone");
        body.GetProperty("email").GetString().ShouldBe(TestUser.Sam.Email);
        body.GetProperty("name").GetString().ShouldBe(TestUser.Sam.Name);
    }

    [Test]
    public async Task Me_WhenSecondUser_ShouldReturnThatUser()
    {
        using var alex = ClientFor(TestUser.Alex);

        var body = await alex.GetFromJsonAsync<JsonElement>("/api/me");

        body.GetProperty("email").GetString().ShouldBe(TestUser.Alex.Email);
        body.GetProperty("name").GetString().ShouldBe(TestUser.Alex.Name);
    }

    // The session cookie the sign-in flow will issue: unreadable by script, not sent on cross-site subrequests, and good
    // for 14 days of inactivity. Whether it is Secure follows the public URL's scheme (PublicOriginTests for https).
    [Test]
    public void Session_ShouldBeHttpOnlyLaxAndFourteenDaysSliding()
    {
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        options.Cookie.HttpOnly.ShouldBeTrue();
        options.Cookie.SameSite.ShouldBe(SameSiteMode.Lax);
        options.ExpireTimeSpan.ShouldBe(TimeSpan.FromDays(14));
        options.SlidingExpiration.ShouldBeTrue();
    }

    // Test hosts, like a plain-HTTP self-host, have an http public URL: a Secure cookie would never come back over it.
    [Test]
    public async Task Session_WhenPublicUrlIsHttp_ShouldNotBeSecure()
    {
        using var browser = Factory.CreateClient();

        var response = await browser.GetAsync($"/{SessionProbeController.PATH}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SessionCookies.Attributes(response).ShouldBe(["path=/", "samesite=lax", "httponly"], ignoreOrder: true);
    }
}
