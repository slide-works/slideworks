using SlideWorks.AcceptanceTests.Pages;

namespace SlideWorks.AcceptanceTests;

// The sign-in page against the real BFF. The acceptance host has no SLIDEWORKS_GOOGLE_CLIENT_ID, so it is the
// not-set-up server: the page says so from GET /api/signin/options instead of offering a button that would 503.
public class SignInPageTests : AcceptanceTest
{
    [Test]
    public async Task SignIn_WhenGoogleNotSetUp_ShouldSaySoAndOfferNoButton()
    {
        var signIn = new SignInPage(Page);

        await signIn.GotoAsync("?returnTo=%2Fdecks");

        await Expect(signIn.NotSetUp).ToHaveTextAsync("Sign-in isn’t set up on this server.");
        await Expect(signIn.GoogleButton).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SignIn_WhenErrorInQuery_ShouldShowItsMessage()
    {
        var signIn = new SignInPage(Page);

        await signIn.GotoAsync("?error=email_linked");

        await Expect(signIn.Error).ToHaveTextAsync("This email is linked to another Google account.");
    }
}
