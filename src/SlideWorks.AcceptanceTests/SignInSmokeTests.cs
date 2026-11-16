using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The skeleton end to end: the real BFF serving the built SPA, the auth gate in App.tsx deciding from GET /api/me, and
// SignIn.AsAsync putting a TestUser's session in the browser.
public partial class SignInSmokeTests : AcceptanceTest
{
    [Test]
    public async Task SignedInPresenter_WhenOpeningDecks_ShouldSeeShell()
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);

        await decks.GotoAsync();

        await Expect(decks.Shell).ToBeVisibleAsync();
        await Expect(decks.Library).ToBeVisibleAsync();
        // The user menu shows the initials of the person /api/me returned, so the browser really is signed in as Sam Rivera.
        await Expect(decks.UserMenu).ToHaveTextAsync("SR");
        await Expect(Page).ToHaveURLAsync(Decks());
    }

    [Test]
    public async Task SignedOutVisitor_WhenOpeningDecks_ShouldLandOnSignIn()
    {
        var decks = new DecksPage(Page);

        await decks.GotoAsync();

        await Expect(Page).ToHaveURLAsync(SignInWithReturnToDecks());
        await Expect(new SignInPage(Page).Root).ToBeVisibleAsync();
        await Expect(decks.Shell).ToHaveCountAsync(0);
    }

    [GeneratedRegex(@"/signin\?returnTo=%2Fdecks$")]
    private static partial Regex SignInWithReturnToDecks();

    [GeneratedRegex(@"/decks$")]
    private static partial Regex Decks();
}
