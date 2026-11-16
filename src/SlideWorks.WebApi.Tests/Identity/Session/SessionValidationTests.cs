using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Identity.Gate;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Session;

// A cookie session follows the person's current access on every request (S2, D8), through the real Cookies scheme
// (SessionCookies.ForAsync), which the X-Test-User header bypasses. Rejected, unknown or unnamed: signed out, with the
// cookie cleared. Approved or made admin: the next request sees it, without a new cookie.
[TestFixture]
public class SessionValidationTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => GateProbeController.AddTo;

    [Test]
    public async Task Session_WhenUserRejected_ShouldBeSignedOut()
    {
        var person = TestUser.NewPending();
        var cookie = await SessionCookies.ForAsync(Factory, person);
        (await GetAsync("/api/me", cookie)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await ChangeAccessAsync(person, (accounts, id) => accounts.RejectAsync(id, by: null));

        var response = await GetAsync("/api/me", cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Not signed in");
        ClearsSession(response).ShouldBeTrue("the cookie should be signed out");
    }

    [Test]
    public async Task Session_WhenUserUnknown_ShouldBeSignedOut()
    {
        var cookie = Forge(
            new Claim(SlideWorksClaims.PRESENTER_ID, Guid.NewGuid().ToString()),
            new Claim(SlideWorksClaims.EMAIL, "gone@team.example"),
            new Claim(SlideWorksClaims.ACTOR_KIND, "Person"),
            new Claim(SlideWorksClaims.ACCESS, "Approved"));

        var response = await GetAsync("/api/me", cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Not signed in");
        ClearsSession(response).ShouldBeTrue("the cookie should be signed out");
    }

    [Test]
    public async Task Session_WhenCookieHasNoPresenterId_ShouldBeSignedOut()
    {
        await SessionCookies.ForAsync(Factory, TestUser.Sam);
        var cookie = Forge(
            new Claim(SlideWorksClaims.EMAIL, TestUser.Sam.Email),
            new Claim(SlideWorksClaims.ACTOR_KIND, "Person"),
            new Claim(SlideWorksClaims.ACCESS, "Approved"));

        var response = await GetAsync("/api/me", cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Not signed in");
        ClearsSession(response).ShouldBeTrue("the cookie should be signed out");
    }

    [Test]
    public async Task Session_WhenApprovedAfterSignIn_ShouldPassOnNextRequest()
    {
        var person = TestUser.NewPending();
        var cookie = await SessionCookies.ForAsync(Factory, person);
        (await GetAsync("/api/decks", cookie)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ChangeAccessAsync(person, (accounts, id) => accounts.ApproveAsync(id, by: null));

        var response = await GetAsync("/api/decks", cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse("the session follows the stored user without a new cookie");
    }

    [Test]
    public async Task Session_WhenMadeAdmin_ShouldSeeAdminOnNextRequest()
    {
        var person = TestUser.NewPending();
        await ChangeAccessAsync(person, (accounts, id) => accounts.ApproveAsync(id, by: null));
        var cookie = await SessionCookies.ForAsync(Factory, person);
        (await GetAsync(GateProbeController.ADMIN_PATH, cookie)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ChangeAccessAsync(person, (accounts, id) => accounts.MakeAdminAsync(id, by: null));

        var probe = await GetAsync(GateProbeController.ADMIN_PATH, cookie);
        var me = await GetAsync("/api/me", cookie);

        probe.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await me.Content.ReadFromJsonAsync<MeAdminDto>())!.IsAdmin.ShouldBeTrue();
    }

    // The person signs in (through TestPeople, as every test seam does) and the change is made the way an admin's would
    // be, through IUserAccounts.
    private async Task ChangeAccessAsync(TestUser person, Func<IUserAccounts, Guid, Task<AccountChange>> change)
    {
        using var scope = Factory.Services.CreateScope();
        var user = await TestPeople.SignInAsync(scope.ServiceProvider, person);
        (await change(scope.ServiceProvider.GetRequiredService<IUserAccounts>(), user.Id)).ShouldBe(AccountChange.Changed);
    }

    // A cookie the app's own key ring sealed, holding exactly these claims: one a session from before this rule, or a
    // person who has since gone, would carry.
    private string Forge(params Claim[] claims)
    {
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            CookieAuthenticationDefaults.AuthenticationScheme);
        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string cookie)
    {
        using var browser = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        return await browser.SendAsync(request);
    }

    // The sw-session cookie is deleted: an empty value that expired at the epoch.
    private static bool ClearsSession(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(value => value.StartsWith("sw-session=;", StringComparison.Ordinal)
                && value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));

    private sealed record MeAdminDto(bool IsAdmin);
}
