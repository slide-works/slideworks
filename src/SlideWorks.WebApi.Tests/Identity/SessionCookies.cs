using System.Net.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity;

public static class SessionCookies
{
    // Reads the sw-session Set-Cookie header's attributes, lower-cased, such as "secure" or "samesite=lax".
    public static IReadOnlyList<string> Attributes(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("sw-session=", StringComparison.Ordinal));
        return header.Split(';').Skip(1).Select(part => part.Trim().ToLowerInvariant()).ToArray();
    }

    // A real session cookie of the app's Cookies scheme for the person ("sw-session=…", ready for a Cookie header), as
    // sign-in would set it: the person signs in through TestPeople and the ticket holds SessionPrincipal.For them. For
    // tests that need the real cookie scheme, which the X-Test-User header bypasses. Throws for a Rejected person.
    public static async Task<string> ForAsync(WebApplicationFactory<ApiMarker> factory, TestUser person)
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestPeople.SignInAsync(scope.ServiceProvider, person);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            SessionPrincipal.For(user, CookieAuthenticationDefaults.AuthenticationScheme),
            CookieAuthenticationDefaults.AuthenticationScheme);
        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }
}
