using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// /signin, where the auth gate sends anyone who isn't signed in: the Google button (when sign-in is set up on the
// server), the request-access hint, and the message for an ?error= code.
public sealed class SignInPage(IPage page)
{
    public const string PATH = "/signin";

    public ILocator Root => page.GetByTestId("signin-page");

    public ILocator GoogleButton => page.GetByTestId("signin-google");

    public ILocator RequestAccessHint => page.GetByTestId("signin-request-hint");

    public ILocator NotSetUp => page.GetByTestId("signin-not-set-up");

    public ILocator Error => page.GetByTestId("signin-error");

    public Task GotoAsync(string query = "") => page.GotoAsync(PATH + query);

    // The auth gate sent the browser here, carrying where it was going (path, query and hash) as returnTo.
    public async Task ShouldBeShownWithReturnToAsync(string returnTo)
    {
        await Expect(page).ToHaveURLAsync(new Regex($"{Regex.Escape(PATH)}\\?returnTo={Regex.Escape(Uri.EscapeDataString(returnTo))}$"));
        await Expect(Root).ToBeVisibleAsync();
    }
}
