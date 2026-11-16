using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// Signs a test in as a TestUser without Google, from the test project only (K1): production has no switch for it. A
// request names its person by email in the X-Test-User header (API tests) or the sw-test-user cookie (a browser in the
// acceptance tests): a named TestUser or a NewPending() email. The person is stored through TestPeople, and the principal
// is SessionPrincipal.For the stored user, as a cookie session's is. A Rejected person is signed out, as the cookie
// scheme signs them out. The app's Cookies scheme forwards here only when a request names a test user,
// so a request without one is still signed out by the real scheme, with its real 401.
//
// The forward covers sign-out too, so POST /api/signout lands here for a test user: it deletes the sw-test-user cookie, and
// the browser's next request is signed out as a real session's would be. A person named by the X-Test-User header can't
// be signed out: the client sends the header again on every request.
//
// This file also compiles into the acceptance project, so it uses only what both test projects reference.
public sealed class TestAuthScheme(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationSignOutHandler
{
    public const string NAME = "Test";

    public const string HEADER = "X-Test-User";

    public const string COOKIE = "sw-test-user";

    // For ConfigureTestServices: registers the scheme and points the app's Cookies scheme at it for requests that name
    // a test user.
    public static void AddTo(IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthScheme>(NAME, _ => { });
        services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, cookies =>
            cookies.ForwardDefaultSelector = context => NamedEmail(context.Request) == null ? null : NAME);
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var email = NamedEmail(Request);
        if (email == null)
        {
            return AuthenticateResult.NoResult();
        }
        var person = TestUser.Find(email);
        if (person == null)
        {
            return AuthenticateResult.Fail($"'{email}' is not a TestUser.");
        }
        var user = await TestPeople.SignInAsync(Context.RequestServices, person);
        if (user.Access == UserAccess.Rejected)
        {
            return AuthenticateResult.NoResult();
        }
        return AuthenticateResult.Success(new AuthenticationTicket(SessionPrincipal.For(user, NAME), NAME));
    }

    // With both a test cookie and a real sw-session, the forward skips the real Cookies handler, so sw-session survives.
    public Task SignOutAsync(AuthenticationProperties? properties)
    {
        Response.Cookies.Delete(COOKIE);
        return Task.CompletedTask;
    }

    private static string? NamedEmail(HttpRequest request)
    {
        var email = request.Headers[HEADER].ToString();
        if (email.Length == 0)
        {
            email = request.Cookies[COOKIE] ?? "";
        }
        return email.Length == 0 ? null : email;
    }
}
