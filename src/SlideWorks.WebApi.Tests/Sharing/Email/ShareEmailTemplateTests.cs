using SlideWorks.WebApi.Features.Sharing.Email;

namespace SlideWorks.WebApi.Tests.Sharing.Email;

[TestFixture]
public class ShareEmailTemplateTests
{
    [Test]
    public void Build_WhenValuesAreHostile_ShouldEncodeEveryValueInHtml()
    {
        var mail = ShareEmailTemplate.Build("to@x.example", "A&B", "a\"b@x.example", "<b>t</b>", "https://h.example/d/1/s?x=\"1\"", "https://h.example/u#t=a&b");

        mail.Html.ShouldNotContain("<b>");
        mail.Html.ShouldContain("A&amp;B");
        mail.Html.ShouldContain("&lt;b&gt;t&lt;/b&gt;");
        mail.Html.ShouldNotContain("x=\"1\"");
        mail.Html.ShouldNotContain("view");
        mail.UnsubscribeUrl.ShouldBe("https://h.example/u#t=a&b");
    }

    [Test]
    public void Clean_WhenCutInsideSurrogatePair_ShouldKeepNoHalfPair()
    {
        var cleaned = ShareEmailTemplate.Clean(new string('a', 59) + char.ConvertFromUtf32(0x1F600), 60, "x");

        cleaned.ShouldBe(new string('a', 59));
    }

    [Test]
    public void Clean_WhenNothingLeft_ShouldUseTheFallback()
    {
        ShareEmailTemplate.Clean(char.ConvertFromUtf32(0x202E) + "\r\n", 60, "Someone").ShouldBe("Someone");
    }
}
