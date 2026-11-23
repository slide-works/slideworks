using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Accounts.Avatars;

namespace SlideWorks.WebApi.Tests.Accounts;

// IAvatars.For builds C2's shape from a stored person: initials, a colour key and the picture to show.
[TestFixture]
public class AvatarsTests
{
    private static readonly IAvatars AVATARS = new Avatars();

    [Test]
    public void For_WhenNoColour_ShouldPickStableKey()
    {
        var user = UserNamed("Sam Rivera");

        var first = AVATARS.For(user).Colour;
        var second = AVATARS.For(user).Colour;

        first.ShouldBe(second);
        AvatarColours.Keys.ShouldContain(first);
        first.ShouldBe(AvatarColours.Keys[(int)(BitConverter.ToUInt32(user.Id.ToByteArray(), 0) % 8)]);
    }

    [Test]
    public void For_WhenColourChosen_ShouldUseIt()
    {
        var user = UserNamed("Sam Rivera");
        user.AvatarColour = "violet";

        AVATARS.For(user).Colour.ShouldBe("violet");
    }

    [TestCase("Sam Rivera", "SR")]
    [TestCase("sam", "S")]
    [TestCase("  mary jane watson ", "MJ")]
    public void For_WhenName_ShouldTakeFirstLettersOfFirstTwoWords(string name, string initials)
    {
        AVATARS.For(UserNamed(name)).Initials.ShouldBe(initials);
    }

    [Test]
    public void For_WhenPhotoAndGooglePicture_ShouldPreferPhoto()
    {
        var user = UserNamed("Sam Rivera");
        user.PictureUrl = "https://pictures.example/g.png";
        user.PhotoId = "abc123";

        AVATARS.For(user).PictureUrl.ShouldBe($"/api/people/{user.Id}/photo/abc123");
    }

    [Test]
    public void For_WhenOnlyGooglePicture_ShouldUseIt()
    {
        var user = UserNamed("Sam Rivera");
        user.PictureUrl = "https://pictures.example/g.png";

        AVATARS.For(user).PictureUrl.ShouldBe("https://pictures.example/g.png");
    }

    [Test]
    public void For_WhenNoPicture_ShouldBeNull()
    {
        AVATARS.For(UserNamed("Sam Rivera")).PictureUrl.ShouldBeNull();
    }

    private static User UserNamed(string name) => new() { Id = Guid.NewGuid(), Email = "sam@team.example", Name = name, GoogleSubject = "sub" };
}
