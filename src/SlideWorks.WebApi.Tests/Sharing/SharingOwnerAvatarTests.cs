using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Identity.Google;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// The owner's avatar in sharing.json when they signed in with Google and have a picture: the picture's URL, with the initials
// and the colour key beside it. Ada is the admin the test hosts bootstrap, so she is approved on her first sign-in.
[TestFixture]
public class SharingOwnerAvatarTests : GoogleSignInFixture
{
    [Test]
    public async Task Get_WhenOwnerHasGooglePicture_ShouldReturnItAsPictureUrl()
    {
        using var browser = Browser();
        using var signedIn = await SignInAsync(browser, "sub-owner-avatar", TestUser.Ada.Email, TestUser.Ada.Name, picture: "https://pictures.example/ada.png");
        var deck = await SharingApi.CreateDeckAsync(browser);

        var view = await browser.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        var owner = view.GetProperty("owner");
        owner.GetProperty("email").GetString().ShouldBe(TestUser.Ada.Email);
        owner.GetProperty("name").GetString().ShouldBe(TestUser.Ada.Name);
        owner.GetProperty("avatar").GetProperty("initials").GetString().ShouldBe("AB");
        owner.GetProperty("avatar").GetProperty("pictureUrl").GetString().ShouldBe("https://pictures.example/ada.png");
    }
}
