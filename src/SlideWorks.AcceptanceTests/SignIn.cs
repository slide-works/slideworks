using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// Signs the page's browser in as a TestUser, without Google: it sets the cookie TestAuthScheme reads (the scheme is only
// on the acceptance host, never in production). It applies to the next request, so sign in before navigating.
public static class SignIn
{
    public static Task AsAsync(IPage page, TestUser user)
    {
        if (user.IsAnonymous)
        {
            throw new ArgumentException("Anonymous signs in as nobody: leave the page signed out instead.", nameof(user));
        }
        return page.Context.AddCookiesAsync(
        [
            new Cookie { Name = TestAuthScheme.COOKIE, Value = user.Email, Url = AcceptanceApp.BaseUrl },
        ]);
    }
}
