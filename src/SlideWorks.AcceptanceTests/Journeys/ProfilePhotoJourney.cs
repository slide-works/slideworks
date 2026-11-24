using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// A person uploads a real PNG through the profile page's file input and sees it as their picture, on the page at once and in
// the user menu after the shell's next load (the shell reads /api/me once per page load). Removing it brings the initials back.
public class ProfilePhotoJourney : AcceptanceTest
{
    [Test]
    public async Task Upload_WhenPng_ShouldShowInUserMenu()
    {
        await SignIn.AsAsync(Page, TestUser.NewApproved());
        var profile = new ProfilePage(Page);
        var menu = new UserMenu(Page);
        await Page.GotoAsync(ProfilePage.PATH);
        await Expect(profile.PhotoSource).ToHaveTextAsync("Picture: Initials");
        await Expect(profile.RemovePhoto).ToHaveCountAsync(0);

        await profile.PhotoInput.SetInputFilesAsync(new FilePayload { Name = "me.png", MimeType = "image/png", Buffer = SampleImage.Png() });

        await Expect(profile.PhotoSource).ToHaveTextAsync("Picture: Your photo");
        await Expect(profile.RemovePhoto).ToBeVisibleAsync();
        var photo = profile.PhotoPicture;
        await Expect(photo).ToBeVisibleAsync();
        await Expect(photo).ToHaveAttributeAsync("src", new Regex("^/api/people/[0-9a-f-]+/photo/[0-9A-Za-z]+$"));

        await Expect(menu.AvatarPicture).ToHaveAttributeAsync("src", new Regex("/photo/[0-9A-Za-z]+$"));

        await profile.RemovePhoto.ClickAsync();
        await Expect(profile.RemovePhoto).ToHaveCountAsync(0);
        await Expect(profile.PhotoSource).ToHaveTextAsync("Picture: Initials");
        await Expect(menu.AvatarPicture).ToHaveCountAsync(0);
    }
}
