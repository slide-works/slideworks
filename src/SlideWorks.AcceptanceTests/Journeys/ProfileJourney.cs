using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// A person changes their display name and avatar colour on /profile; the top bar's user menu shows them at once and
// after a reload.
public class ProfileJourney : AcceptanceTest
{
    [Test]
    public async Task ChangeName_WhenSaved_ShouldShowInUserMenu()
    {
        var person = TestUser.NewApproved();
        await SignIn.AsAsync(Page, person);
        var profile = new ProfilePage(Page);
        var menu = new UserMenu(Page);
        await Page.GotoAsync(ProfilePage.PATH);
        await Expect(profile.NameInput).ToHaveValueAsync(person.Name);
        await Expect(profile.Save).ToBeDisabledAsync();
        var oldInitials = await menu.AvatarInitials.InnerTextAsync();

        await profile.NameInput.FillAsync("Quinn Ames");
        await profile.ColourOption("pink").ClickAsync();
        await Expect(profile.Save).ToBeEnabledAsync();
        await profile.Save.ClickAsync();

        await Expect(profile.Name).ToHaveTextAsync("Quinn Ames");
        await Expect(profile.Save).ToBeDisabledAsync();
        await Expect(menu.AvatarInitials).ToHaveTextAsync("QA");
        await Expect(menu.Avatar).ToHaveAttributeAsync("data-colour", "pink");
        await Expect(menu.Button).ToHaveAttributeAsync("aria-label", "Account: Quinn Ames");
        oldInitials.ShouldNotBe("QA");

        await Page.ReloadAsync();

        await Expect(profile.NameInput).ToHaveValueAsync("Quinn Ames");
        await Expect(menu.AvatarInitials).ToHaveTextAsync("QA");
        await Expect(menu.Avatar).ToHaveAttributeAsync("data-colour", "pink");
        await Expect(profile.ColourOption("pink")).ToHaveAttributeAsync("aria-checked", "true");
    }
}
