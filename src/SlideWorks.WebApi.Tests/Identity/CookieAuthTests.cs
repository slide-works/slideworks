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
        JsonShape.AssertMatches(body, "me.json", "pictureUrl");
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

    // The session cookie the sign-in flow will issue: unreadable by script, not sent on cross-site subrequests, Secure
    // whenever the request is HTTPS, and good for 14 days of inactivity.
    [Test]
    public void Session_ShouldBeHttpOnlyLaxAndFourteenDaysSliding()
    {
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        options.Cookie.HttpOnly.ShouldBeTrue();
        options.Cookie.SameSite.ShouldBe(SameSiteMode.Lax);
        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.SameAsRequest);
        options.ExpireTimeSpan.ShouldBe(TimeSpan.FromDays(14));
        options.SlidingExpiration.ShouldBeTrue();
    }
}
