using System.Text.RegularExpressions;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The skeleton end to end: the real BFF serving the built SPA, the auth gate in App.tsx deciding from GET /api/me, and
// SignIn.AsAsync putting a TestUser's session in the browser.
public partial class SignInSmokeTests : AcceptanceTest
{
    [Test]
    public async Task SignedInPresenter_WhenOpeningDecks_ShouldSeeShell()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);

        await Page.GotoAsync("/decks");

        await Expect(Page.GetByTestId("app-shell")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("decks-page")).ToBeVisibleAsync();
        // The user menu shows the initials of the person /api/me returned, so the browser really is signed in as Sam Rivera.
        await Expect(Page.GetByTestId("user-menu")).ToHaveTextAsync("SR");
        Page.Url.ShouldEndWith("/decks");
    }

    [Test]
    public async Task SignedOutVisitor_WhenOpeningDecks_ShouldLandOnSignIn()
    {
        await Page.GotoAsync("/decks");

        await Expect(Page).ToHaveURLAsync(SignInWithReturnToDecks());
        await Expect(Page.GetByTestId("signin-page")).ToBeVisibleAsync();
        (await Page.GetByTestId("app-shell").CountAsync()).ShouldBe(0);
    }

    [GeneratedRegex(@"/signin\?returnTo=%2Fdecks$")]
    private static partial Regex SignInWithReturnToDecks();
}
