using System.Text;
using System.Text.Json;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Tests.Templates;

// The logo and art checks (D6). Hostile files are committed under Fixtures/hostile-svg, one per case; a refused file is
// refused whole, never cleaned, and the first finding names the code.
[TestFixture]
public class SvgCheckTests
{
    private const string PNG_BASE64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    [Test]
    public async Task Check_WhenSvgHasScript_ShouldRefuse()
    {
        var result = await CheckAsync(Fixture("script"));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_script");
        result.ContentType.ShouldBeNull();
    }

    [TestCase("event-handler")]
    [TestCase("event-handler-onload")]
    public async Task Check_WhenSvgHasAnEventHandler_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_script");
        result.MetaJson.ShouldBeNull();
    }

    [TestCase("external-xlink-http")]
    [TestCase("external-style-url")]
    [TestCase("external-import")]
    [TestCase("external-data-image")]
    [TestCase("xml-base")]
    public async Task Check_WhenSvgHasAnExternalReference_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_external_reference");
        result.MetaJson.ShouldBeNull();
    }

    [TestCase("js-xlink")]
    [TestCase("js-href")]
    [TestCase("js-mixed-case")]
    [TestCase("js-tab")]
    [TestCase("js-newline")]
    public async Task Check_WhenHrefIsJavascript_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_script");
        result.ErrorValues.ShouldBeEmpty();
    }

    [TestCase("animate-href")]
    [TestCase("animate-xlink-href")]
    public async Task Check_WhenAnimateTargetsHref_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_script");
        result.Errors.ShouldBeEmpty();
    }

    [TestCase("use-outside")]
    [TestCase("use-data")]
    [TestCase("feimage-outside")]
    [TestCase("anchor-outside")]
    public async Task Check_WhenUseOrFeImagePointsOutside_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_external_reference");
        result.ErrorValues.ShouldBeEmpty();
    }

    [TestCase("style-element-url")]
    [TestCase("style-element-escaped")]
    [TestCase("style-element-import")]
    [TestCase("style-element-split-by-comment")]
    [TestCase("style-element-image-set")]
    [TestCase("style-element-font-src")]
    public async Task Check_WhenAStyleElementLoadsAUrl_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_external_reference");
        result.Errors.ShouldBeEmpty();
    }

    [TestCase("presentation-url")]
    [TestCase("presentation-escaped-url")]
    public async Task Check_WhenAPresentationAttributeLoadsAUrl_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_external_reference");
        result.ContentType.ShouldBeNull();
    }

    [Test]
    public async Task Check_WhenScriptIsInAnotherNamespace_ShouldRefuse()
    {
        var result = await CheckAsync(Fixture("script-other-namespace"));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_script");
        result.ContentType.ShouldBeNull();
    }

    [Test]
    public async Task Check_WhenItHasAStylesheetInstruction_ShouldRefuse()
    {
        var result = await CheckAsync(Fixture("stylesheet-instruction"));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_external_reference");
        result.ContentType.ShouldBeNull();
    }

    [Test]
    public async Task Check_WhenItHasADoctype_ShouldRefuse()
    {
        var result = await CheckAsync(Fixture("doctype"));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_external_reference");
        result.ContentType.ShouldBeNull();
    }

    [Test]
    public async Task Check_WhenSvgDeclaresAnEntity_ShouldRefuse()
    {
        var result = await CheckAsync(Fixture("entity"));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_external_reference");
        result.ContentType.ShouldBeNull();
    }

    [TestCase("iframe")]
    [TestCase("object")]
    [TestCase("embed")]
    public async Task Check_WhenItEmbedsAnIframeOrObject_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_foreign_object");
        result.ContentType.ShouldBeNull();
    }

    [Test]
    public async Task Check_WhenSvgHasForeignObject_ShouldRefuse()
    {
        var result = await CheckAsync(Fixture("foreign-object"));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_foreign_object");
        result.ContentType.ShouldBeNull();
    }

    [Test]
    public async Task Check_WhenSvgIsUtf16_ShouldStillBeChecked()
    {
        var hostile = await CheckAsync(Fixture("utf16-script"));
        var plain = await CheckAsync(Fixture("utf16-plain"));

        hostile.ErrorCode.ShouldBe("svg_script");
        plain.Ok.ShouldBeTrue();
        plain.ContentType.ShouldBe("image/svg+xml");
    }

    [Test]
    public async Task Check_WhenSvgIsPlainShapes_ShouldPass()
    {
        var bytes = Fixture("plain-shapes");

        var result = await CheckAsync(bytes);

        result.Ok.ShouldBeTrue();
        result.ContentType.ShouldBe("image/svg+xml");
        result.MetaJson.ShouldBeNull();
        result.ErrorCode.ShouldBeNull();
    }

    [Test]
    public async Task Check_WhenWebp_ShouldPass()
    {
        var result = await CheckAsync(Webp());

        result.Ok.ShouldBeTrue();
        result.ContentType.ShouldBe("image/webp");
        result.MetaJson.ShouldBe("""{"width":16,"height":8}""");
    }

    [Test]
    public async Task Check_WhenLosslessWebpOrExtendedWebp_ShouldCarryItsSize()
    {
        var lossless = Riff("VP8L", [0x2F, 0x0F, 0xC0, 0x01, 0x00]);
        var extended = Riff("VP8X", [0, 0, 0, 0, 99, 0, 0, 49, 0, 0]);

        (await CheckAsync(lossless)).MetaJson.ShouldBe("""{"width":16,"height":8}""");
        (await CheckAsync(extended)).MetaJson.ShouldBe("""{"width":100,"height":50}""");
    }

    [Test]
    public async Task Check_WhenNamedSvgButPng_ShouldPassAsPng()
    {
        var result = await CheckAsync(Convert.FromBase64String(PNG_BASE64), "logo.svg");

        result.Ok.ShouldBeTrue();
        result.ContentType.ShouldBe("image/png");
    }

    [Test]
    public async Task Check_WhenPng_ShouldCarryItsPixelSize()
    {
        var result = await CheckAsync(Convert.FromBase64String(PNG_BASE64));

        result.MetaJson.ShouldNotBeNull();
        using var meta = JsonDocument.Parse(result.MetaJson);
        meta.RootElement.GetProperty("width").GetInt32().ShouldBe(1);
        meta.RootElement.GetProperty("height").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task Check_WhenAPngIsTruncatedAfterItsSignature_ShouldPassWithoutASize()
    {
        var result = await CheckAsync(Convert.FromBase64String(PNG_BASE64)[..10]);

        result.Ok.ShouldBeTrue();
        result.MetaJson.ShouldBeNull();
    }

    [TestCase(new byte[0])]
    [TestCase(new byte[] { 0x00, 0x01, 0x02, 0x03 })]
    [TestCase(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 })]
    [TestCase(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })]
    public async Task Check_WhenBytesAreNotAnImage_ShouldRefuse(byte[] bytes)
    {
        var result = await CheckAsync(bytes);

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("not_an_image");
        TemplateRefusal.Of(result.ErrorCode!, null, result.ErrorValues.ToArray()).Message.ShouldBe("Logos and art must be SVG, PNG or WebP files.");
    }

    [Test]
    public async Task Check_WhenTextIsNotXml_ShouldRefuseAsNotAnImage()
    {
        var result = await CheckAsync("hello, I am a logo"u8.ToArray());

        result.ErrorCode.ShouldBe("not_an_image");
    }

    [TestCase("malformed")]
    [TestCase("wrong-root")]
    [TestCase("no-namespace")]
    public async Task Check_WhenSvgIsMalformed_ShouldRefuse(string fixture)
    {
        var result = await CheckAsync(Fixture(fixture));

        result.Ok.ShouldBeFalse();
        result.ErrorCode.ShouldBe("svg_malformed");
        result.ContentType.ShouldBeNull();
    }

    [Test]
    public async Task Check_WhenTheKindIsArt_ShouldApplyTheSameRules()
    {
        var hostile = await new ArtCheck().CheckAsync(new MemoryStream(Fixture("script")), "art.svg", CancellationToken.None);
        var plain = await new ArtCheck().CheckAsync(new MemoryStream(Fixture("plain-shapes")), "art.svg", CancellationToken.None);

        hostile.ErrorCode.ShouldBe("svg_script");
        plain.Ok.ShouldBeTrue();
        new ArtCheck().Kind.ShouldBe(TemplateAssetKind.Art);
        new LogoCheck().Kind.ShouldBe(TemplateAssetKind.Logo);
    }

    [Test]
    public async Task Check_WhenARefusalIsMade_ShouldUseTheMockupWording()
    {
        var result = await CheckAsync(Fixture("script"));

        TemplateRefusal.Of(result.ErrorCode!, null, result.ErrorValues.ToArray()).Message
            .ShouldBe("This SVG contains a script. Export it again without scripts, or upload a PNG.");
    }

    private static Task<AssetCheckResult> CheckAsync(byte[] bytes, string fileName = "mark.svg")
        => new LogoCheck().CheckAsync(new MemoryStream(bytes), fileName, CancellationToken.None);

    // A lossy WebP header for a 16 x 8 picture (the bytes after it are not a picture; only the header is read).
    private static byte[] Webp()
        => Riff("VP8 ", [0x00, 0x00, 0x00, 0x9D, 0x01, 0x2A, 0x10, 0x00, 0x08, 0x00]);

    private static byte[] Riff(string chunk, byte[] data)
    {
        var body = Encoding.ASCII.GetBytes("WEBP" + chunk).Concat(BitConverter.GetBytes(data.Length)).Concat(data).ToArray();
        return [.. "RIFF"u8, .. BitConverter.GetBytes(body.Length), .. body];
    }

    private static byte[] Fixture(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
            {
                return File.ReadAllBytes(Path.Combine(directory.FullName, "SlideWorks.WebApi.Tests", "Templates", "Fixtures", "hostile-svg", $"{name}.svg"));
            }
        }
        throw new InvalidOperationException($"No SlideWorks.slnx above {AppContext.BaseDirectory}.");
    }
}
