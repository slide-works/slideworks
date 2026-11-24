using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Tests.Identity.Google;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Accounts;

// GET /api/me's avatar and time zone for a person who signed in with Google and has a picture.
[TestFixture]
public class MeAvatarTests : GoogleSignInFixture
{
    [Test]
    public async Task Get_WhenSignedInWithGooglePicture_ShouldReturnAvatarAndTimeZone()
    {
        using var browser = Browser();
        using var response = await SignInAsync(browser, "sub-avatar", "sam.rivera@example.test", "Sam Rivera", picture: "https://pictures.example/g.png");

        var me = await MeAsync(browser);

        var avatar = me.GetProperty("avatar");
        avatar.GetProperty("initials").GetString().ShouldBe("SR");
        AvatarColours.Keys.ShouldContain(avatar.GetProperty("colour").GetString()!);
        avatar.GetProperty("pictureUrl").GetString().ShouldBe("https://pictures.example/g.png");
        me.GetProperty("timeZone").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        JsonShape.AssertMatches(me, "me.json", ScreenShapes.Me);
    }
}
