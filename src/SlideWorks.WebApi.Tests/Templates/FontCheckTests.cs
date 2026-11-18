using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

[TestFixture]
public class FontCheckTests
{
    private FakeRenderEngine _engine = null!;
    private FontCheck _check = null!;

    [SetUp]
    public void SetUp()
    {
        _engine = new FakeRenderEngine();
        _check = new FontCheck(new FontMeasure(_engine));
    }

    [Test]
    public async Task Check_WhenBytesAreNotWoff2_ShouldRefuse()
    {
        var result = await CheckAsync(FontFixtures.NotAFont(), "inter.woff2");

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("font_not_woff2");
        result.ErrorValues.ShouldBe([("found", "TrueType")]);
        _engine.MeasureCalls.ShouldBe(0);
    }

    [TestCase(new byte[] { 0x00, 0x01, 0x00, 0x00 }, "TrueType")]
    [TestCase(new byte[] { 0x74, 0x72, 0x75, 0x65 }, "TrueType")]
    [TestCase(new byte[] { 0x4F, 0x54, 0x54, 0x4F }, "OpenType")]
    [TestCase(new byte[] { 0x77, 0x4F, 0x46, 0x46 }, "woff 1")]
    [TestCase(new byte[] { 0x01, 0x02, 0x03, 0x04 }, "not a font")]
    [TestCase(new byte[] { 0x77 }, "not a font")]
    [TestCase(new byte[0], "not a font")]
    public async Task Check_WhenTheSignatureIsKnown_ShouldNameIt(byte[] head, string found)
    {
        var result = await CheckAsync([.. head, .. new byte[64]], "font.woff2");

        result.ErrorCode.ShouldBe("font_not_woff2");
        result.ErrorValues.ShouldBe([("found", found)]);
    }

    [Test]
    public async Task Check_WhenNamedWoff2ButTrueType_ShouldRefuse()
    {
        var result = await CheckAsync(FontFixtures.NotAFont(), "inter.woff2");

        result.ErrorCode.ShouldBe("font_not_woff2");
        TemplateRefusal.Of(result.ErrorCode!, null, result.ErrorValues.ToArray()).Message.ShouldBe("Fonts must be woff2 files. This one is TrueType.");
    }

    [Test]
    public async Task Check_WhenTheNameSaysTtfButTheBytesAreWoff2_ShouldPass()
    {
        var result = await CheckAsync(FontFixtures.Normal(), "inter.ttf");

        result.Ok.ShouldBeTrue();
    }

    [TestCase(0.74, "too wide", "0.74", "0.70")]
    [TestCase(0.44, "too narrow", "0.44", "0.45")]
    public async Task Check_WhenWidthIsOutsideTheRange_ShouldRefuse(double em, string problem, string width, string limit)
    {
        _engine.MeasuredRaw = RawFor(em);

        var result = await CheckAsync(FontFixtures.Normal(), "a.woff2");

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("font_width_out_of_range");
        result.ErrorValues.ShouldBe([("problem", problem), ("width", width), ("limit", limit)]);
    }

    [Test]
    public async Task Check_WhenTooWide_ShouldReadAsTheMockupDoes()
    {
        _engine.MeasuredRaw = RawFor(0.74);

        var result = await CheckAsync(FontFixtures.Normal(), "a.woff2");

        TemplateRefusal.Of(result.ErrorCode!, null, result.ErrorValues.ToArray()).Message.ShouldBe("This font is too wide for slide text (0.74 em; the limit is 0.70).");
    }

    [TestCase(0.45)]
    [TestCase(0.70)]
    public async Task Check_WhenWidthIsOnTheLimit_ShouldPass(double em)
    {
        _engine.MeasuredRaw = RawFor(em);

        var result = await CheckAsync(FontFixtures.Normal(), "a.woff2");

        result.Ok.ShouldBeTrue();
    }

    [Test]
    public async Task Check_WhenItPasses_ShouldCarryTheWidthInMeta()
    {
        var result = await CheckAsync(FontFixtures.Normal(), "a.woff2");

        result.Ok.ShouldBeTrue();
        result.ContentType.ShouldBe("font/woff2");
        result.MetaJson.ShouldBe("{\"avgCharEm\":0.56}");
        result.ErrorCode.ShouldBeNull();
        _engine.MeasureCalls.ShouldBe(1);
    }

    [Test]
    public async Task Check_WhenTheBrowserIsUnavailable_ShouldRefuse()
    {
        _engine.ThrowOnMeasure = new RendererUnavailableException("no chromium");

        var result = await CheckAsync(FontFixtures.Normal(), "a.woff2");

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("renderer_unavailable");
    }

    [Test]
    public async Task Check_WhenTheMeasureTimesOut_ShouldRefuse()
    {
        _engine.ThrowOnMeasure = new TimeoutException("slow");

        var result = await CheckAsync(FontFixtures.Normal(), "a.woff2");

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("renderer_unavailable");
    }

    [Test]
    public async Task Check_WhenTheBrowserCannotDecodeTheFont_ShouldRefuse()
    {
        _engine.ThrowOnMeasure = new InvalidDataException("bad");

        var result = await CheckAsync(FontFixtures.Normal(), "a.woff2");

        result.ErrorCode.ShouldBe("font_not_woff2");
    }

    [Test]
    public void Kind_ShouldBeFont()
    {
        _check.Kind.ShouldBe(TemplateAssetKind.Font);
    }

    [Test]
    public void SampleLine_ShouldBe210Characters()
    {
        FontMeasurePage.SAMPLE.Length.ShouldBe(210);
    }

    [Test]
    public void Html_WhenTheFontHoldsMarkup_ShouldKeepItOutOfThePage()
    {
        var hostile = System.Text.Encoding.UTF8.GetBytes("wOF2\"); } </style><script>alert(1)</script>");

        var html = FontMeasurePage.Html(hostile);

        html.ShouldNotContain("<script");
        html.ShouldNotContain("alert(1)");
        html.ShouldContain("base64,");
    }

    private static double RawFor(double em) => em / FontMeasure.BUILT_IN_EM * FontMeasure.REFERENCE;

    private async Task<AssetCheckResult> CheckAsync(byte[] bytes, string fileName)
    {
        using var stream = new MemoryStream(bytes);
        return await _check.CheckAsync(stream, fileName, CancellationToken.None);
    }
}
