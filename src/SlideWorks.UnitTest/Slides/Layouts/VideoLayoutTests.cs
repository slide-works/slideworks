using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class VideoLayoutTests
{
    private const string WATCH = "https://www.youtube.com/watch?v=M7lc1UVf-VE";

    [Test]
    public void Validate_WhenTheUrlIsAWatchLink_ShouldAcceptIt()
    {
        Validate($$"""{"headline":"Watch the slice","url":"{{WATCH}}","start":12,"play":"auto","caption":"Now"}""")
            .Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheUrlIsNotYouTube_ShouldRefuse()
    {
        Validate("""{"headline":"Watch the slice","url":"https://example.com/watch?v=M7lc1UVf-VE"}""")
            .Errors.ShouldHaveSingleItem().ShouldStartWith("video.url: must be a link to one YouTube video");
    }

    [Test]
    public void Validate_WhenTheUrlIsJavascript_ShouldRefuse()
    {
        Validate("""{"headline":"Watch the slice","url":"javascript:alert(1)"}""")
            .Errors.ShouldHaveSingleItem().ShouldContain("http or https");
    }

    [Test]
    public void Validate_WhenTheHostOnlyLooksLikeYouTube_ShouldRefuse()
    {
        Validate("""{"headline":"Watch the slice","url":"https://www.youtube.com.evil.com/watch?v=M7lc1UVf-VE"}""")
            .Errors.ShouldHaveSingleItem().ShouldStartWith("video.url: ");
    }

    [Test]
    public void Validate_WhenStartIsNotAWholeNumber_ShouldRefuse()
    {
        Validate($$"""{"headline":"Watch the slice","url":"{{WATCH}}","start":1.5}""")
            .Errors.ShouldHaveSingleItem().ShouldStartWith("video.start: ");
    }

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate("video", document.RootElement);
    }
}
