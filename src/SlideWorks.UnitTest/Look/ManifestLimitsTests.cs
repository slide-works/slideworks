using SlideWorks.Core.Look.Manifest;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// A manifest's counts against TemplateLimits, and its ornament dots' token names (ManifestLimits).
public class ManifestLimitsTests
{
    private static TemplateManifest Sample() =>
        ManifestJson.Parse(File.ReadAllText(ContractFiles.PathOf("template-manifest.sample.json"))).Manifest!;

    private static TemplateManifest WithFonts(int count)
    {
        var sample = Sample();
        return sample with { Fonts = Enumerable.Range(0, count).Select(index => sample.Fonts[0] with { File = $"fonts/f{index}.woff2" }).ToList() };
    }

    private static TemplateManifest WithLayouts(int count) =>
        Sample() with { CustomLayouts = Enumerable.Range(0, count).Select(index => new ManifestCustomLayout { Name = $"layout-{index}", File = $"layouts/layout-{index}.json" }).ToList() };

    private static TemplateManifest WithDots(params string[] dots) => Sample() with { Ornament = new ManifestOrnament { Dots = dots } };

    [Test]
    public void Check_WhenTheSampleIsChecked_ShouldPass()
    {
        ManifestLimits.Check(Sample()).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenNineFonts_ShouldRefuse()
    {
        var findings = ManifestLimits.Check(WithFonts(TemplateLimits.MAX_FONT_FILES + 1));

        findings.ShouldBe([new ManifestFinding("too_many_fonts", "fonts", "A template can hold at most 8 font files.")]);
    }

    [Test]
    public void Check_WhenEightFonts_ShouldPass()
    {
        ManifestLimits.Check(WithFonts(TemplateLimits.MAX_FONT_FILES)).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenThirteenLayouts_ShouldRefuse()
    {
        var findings = ManifestLimits.Check(WithLayouts(TemplateLimits.MAX_CUSTOM_LAYOUTS + 1));

        findings.ShouldBe([new ManifestFinding("too_many_layouts", "customLayouts", "A template can hold at most 12 custom layouts.")]);
        ManifestLimits.Check(WithLayouts(TemplateLimits.MAX_CUSTOM_LAYOUTS)).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenSevenDots_ShouldRefuse()
    {
        var findings = ManifestLimits.Check(WithDots("accent", "accent-2", "accent-glow", "good", "warn", "bad", "accent"));

        findings.ShouldBe([new ManifestFinding("manifest_invalid", "ornament.dots", "The template manifest is not valid: ornament.dots can hold at most 6 dots")]);
        ManifestLimits.Check(WithDots("accent", "accent-2", "accent-glow", "good", "warn", "bad")).ShouldBeEmpty();
    }

    [TestCase("ink")]
    [TestCase("paper")]
    [TestCase("extra-5")]
    [TestCase("Accent")]
    [TestCase("accent ")]
    [TestCase("--sw-accent")]
    [TestCase("accent); background: url(x")]
    [TestCase("")]
    public void Check_WhenADotIsNotADrawableToken_ShouldRefuse(string dot)
    {
        var findings = ManifestLimits.Check(WithDots("accent", dot, "extra-4"));

        findings.Select(finding => (finding.Code, finding.Path)).ShouldBe([("manifest_invalid", "ornament.dots[1]")]);
        findings[0].Message.ShouldBe("The template manifest is not valid: ornament.dots[1] must be one of: accent, accent-2, accent-glow, good, warn, bad, extra-1, extra-2, extra-3, extra-4");
    }

    [Test]
    public void Check_WhenEveryDrawableTokenIsUsed_ShouldPass()
    {
        ManifestLimits.Check(WithDots("accent", "accent-2", "accent-glow", "good", "warn", "bad")).ShouldBeEmpty();
        ManifestLimits.Check(WithDots("extra-1", "extra-2", "extra-3", "extra-4")).ShouldBeEmpty();
        ManifestLimits.Check(WithDots()).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenEverythingIsOverItsLimit_ShouldAnswerWithOneFindingEach()
    {
        var huge = WithLayouts(5000) with
        {
            Fonts = WithFonts(5000).Fonts,
            Ornament = new ManifestOrnament { Dots = Enumerable.Repeat("</script>", 5000).ToList() },
        };

        ManifestLimits.Check(huge).Select(finding => (finding.Code, finding.Path)).ShouldBe(
        [
            ("too_many_fonts", "fonts"),
            ("too_many_layouts", "customLayouts"),
            ("manifest_invalid", "ornament.dots"),
        ]);
    }
}
