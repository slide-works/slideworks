using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

[TestFixture]
public class FontFixturesTests
{
    [Test]
    public void Normal_WhenRead_ShouldStartWithTheWoff2Signature()
    {
        var bytes = FontFixtures.Normal();

        bytes.Length.ShouldBe(23_664);
        bytes[..4].ShouldBe("wOF2"u8.ToArray());
        Path.IsPathFullyQualified(FontFixtures.NormalPath).ShouldBeTrue();
    }

    [Test]
    public void Wide_WhenRead_ShouldDifferFromNormal()
    {
        var wide = FontFixtures.Wide();

        wide[..4].ShouldBe("wOF2"u8.ToArray());
        wide.SequenceEqual(FontFixtures.Normal()).ShouldBeFalse();
        Path.IsPathFullyQualified(FontFixtures.WidePath).ShouldBeTrue();
    }

    [Test]
    public void NotAFont_WhenRead_ShouldBeATrueTypeHeaderThenZeros()
    {
        var bytes = FontFixtures.NotAFont();

        bytes[..4].ShouldBe(new byte[] { 0, 1, 0, 0 });
        bytes[4..].ShouldAllBe(b => b == 0);
    }
}
