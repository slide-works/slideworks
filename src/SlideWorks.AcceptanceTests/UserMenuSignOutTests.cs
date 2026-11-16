using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// Sign-out end to end in a real browser: the user menu's Sign out posts /api/signout through the real BFF, and the
// session is gone for the SPA, for the API and across a reload.
public partial class UserMenuSignOutTests : AcceptanceTest
{
    [Test]
    public async Task SignOut_WhenThroughUserMenu_ShouldEndSessionAndLandOnSignIn()
    {
        var decks = new DecksPage(Page);
        var menu = new UserMenu(Page);
        var signIn = new SignInPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await Expect(decks.UserMenu).ToHaveTextAsync("SR");

        await menu.OpenAsync();
        await Expect(menu.Panel).ToContainTextAsync(TestUser.Sam.Email);
        await menu.SignOutAsync();

        await Expect(Page).ToHaveURLAsync(SignInPath());
        await Expect(signIn.Root).ToBeVisibleAsync();
        await Expect(decks.Shell).ToHaveCountAsync(0);
        var me = await Page.APIRequest.GetAsync("/api/me");
        me.Status.ShouldBe(401);

        await Page.ReloadAsync();

        await Expect(Page).ToHaveURLAsync(SignInPath());
        await Expect(signIn.Root).ToBeVisibleAsync();
        await Expect(decks.Shell).ToHaveCountAsync(0);
    }

    [GeneratedRegex(@"/signin$")]
    private static partial Regex SignInPath();
}
