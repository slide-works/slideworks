using SlideWorks.Core.Look;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// A custom palette is held to the built-in contrast pairs (ManifestPaletteCheck) and its extra colours each bring an
// ink that reads on them (ExtraColours).
public class ManifestPaletteCheckTests
{
    private static TemplateManifest Sample() =>
        ManifestJson.Parse(File.ReadAllText(ContractFiles.PathOf("template-manifest.sample.json"))).Manifest!;

    private static TemplateManifest WithToken(string key, string colour)
    {
        var sample = Sample();
        var tokens = new Dictionary<string, string>(sample.Palette!.Tokens) { [key] = colour };
        return sample with { Palette = sample.Palette with { Tokens = tokens } };
    }

    private static TemplateManifest WithExtras(params ManifestExtra[] extras)
    {
        var sample = Sample();
        return sample with { Palette = sample.Palette! with { Extras = extras } };
    }

    private static ManifestExtra Extra(string name, string colour = "#050814", string ink = "#ffffff") =>
        new() { Name = name, Colour = colour, Ink = ink };

    [Test]
    public void Check_WhenTheSampleIsChecked_ShouldPass()
    {
        ManifestPaletteCheck.Check(Sample()).ShouldBeEmpty();
        ExtraColours.Check(Sample()).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenAPairFailsContrast_ShouldNameThePair()
    {
        // The deep-dive pill's text in a mid grey on the sample's #c2410c pill.
        var findings = ManifestPaletteCheck.Check(WithToken("on-dd", "#9ca3af"));

        var ratio = Contrast.Ratio("#9ca3af", "#c2410c");
        var shown = (Math.Floor(ratio * 10) / 10).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        findings.ShouldBe([new ManifestFinding("palette_contrast", "palette.on-dd", $"deep-dive-pill needs 4.5:1; these colours give {shown}:1.")]);
        ratio.ShouldBeLessThan(4.5);
    }

    [Test]
    public void Check_WhenAccent2FailsAsAFill_ShouldRefuse()
    {
        // White text on a pale second accent: accent itself still passes.
        var findings = ManifestPaletteCheck.Check(WithToken("accent-2", "#a5f3fc"));

        findings.Count.ShouldBe(1);
        findings[0].Code.ShouldBe("palette_contrast");
        findings[0].Path.ShouldBe("palette.on-accent");
        findings[0].Message.ShouldStartWith("on-accent-2 needs 4.5:1; these colours give 1.");
    }

    [Test]
    public void Check_WhenSeveralPairsFail_ShouldListEachInTheCodesOrder()
    {
        var findings = ManifestPaletteCheck.Check(WithToken("paper", "#2f5bea"));

        findings.Select(finding => finding.Message.Split(' ')[0]).ShouldBe(["body", "secondary", "headline-accent"]);
        findings.Single(finding => finding.Path == "palette.accent").Message.ShouldContain("needs 3.0:1");
    }

    [Test]
    public void Check_WhenThePaletteIsABuiltInCopy_ShouldPass()
    {
        var builtIns = new PaletteCatalogue().All;
        builtIns.ShouldNotBeEmpty();

        foreach (var builtIn in builtIns)
        {
            var copy = Sample() with { Palette = new ManifestPalette { Tokens = builtIn.Tokens, Extras = [] }, LockPalette = true };

            ManifestPaletteCheck.Check(copy).ShouldBeEmpty(builtIn.Name);
            ExtraColours.Check(copy).ShouldBeEmpty(builtIn.Name);
        }
    }

    [Test]
    public void Check_WhenThereIsNoPalette_ShouldPass()
    {
        var bare = Sample() with { Palette = null, LockPalette = false };

        ManifestPaletteCheck.Check(bare).ShouldBeEmpty();
        ExtraColours.Check(bare).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenLockedWithoutAPalette_ShouldRefuse()
    {
        var locked = Sample() with { Palette = null, LockPalette = true };

        var findings = ManifestPaletteCheck.Check(locked);

        findings.Count.ShouldBe(1);
        (findings[0].Code, findings[0].Path).ShouldBe(("manifest_invalid", "lockPalette"));
        findings[0].Message.ShouldBe("The template manifest is not valid: lockPalette is true, so the template must bring a palette");
    }

    [Test]
    public void Check_WhenLockedWithAPalette_ShouldPass()
    {
        ManifestPaletteCheck.Check(Sample() with { LockPalette = true }).ShouldBeEmpty();
    }

    // A manifest built in code has not been through ManifestJson.Parse: the check refuses it and does not throw.
    [TestCase("red")]
    [TestCase("#fff")]
    [TestCase("rgb(0 0 0)")]
    [TestCase("#2f5bea; color: red")]
    [TestCase("")]
    public void Check_WhenATokenIsNotHex_ShouldRefuseWithoutMeasuring(string colour)
    {
        var findings = ManifestPaletteCheck.Check(WithToken("ink", colour));

        findings.Select(finding => (finding.Code, finding.Path)).ShouldBe([("manifest_invalid", "palette.ink")]);
        findings[0].Message.ShouldNotContain("red");
    }

    [Test]
    public void Check_WhenATokenIsMissing_ShouldRefuseWithoutMeasuring()
    {
        var sample = Sample();
        var tokens = sample.Palette!.Tokens.Where(token => token.Key != "night").ToDictionary(token => token.Key, token => token.Value);

        var findings = ManifestPaletteCheck.Check(sample with { Palette = sample.Palette with { Tokens = tokens } });

        findings.Select(finding => (finding.Code, finding.Path)).ShouldBe([("manifest_invalid", "palette.night")]);
    }

    [Test]
    public void Check_WhenAnExtraHasNoReadableInk_ShouldRefuse()
    {
        // #767676 on white is 4.54:1 and passes; one shade lighter does not.
        var findings = ExtraColours.Check(WithExtras(Extra("extra-1", "#ffffff", "#767676"), Extra("extra-2", "#ffffff", "#777777")));

        findings.Count.ShouldBe(1);
        (findings[0].Code, findings[0].Path).ShouldBe(("palette_contrast", "palette.extras[1].ink"));
        findings[0].Message.ShouldBe("extra-2 needs 4.5:1; these colours give 4.4:1.");
    }

    [Test]
    public void Check_WhenAnExtraNameRepeats_ShouldRefuse()
    {
        var findings = ExtraColours.Check(WithExtras(Extra("extra-2"), Extra("extra-1"), Extra("extra-2")));

        findings.Select(finding => (finding.Code, finding.Path)).ShouldBe([("manifest_invalid", "palette.extras[2].name")]);
    }

    [Test]
    public void Check_WhenFiveExtras_ShouldRefuse()
    {
        var five = WithExtras(Extra("extra-1"), Extra("extra-2"), Extra("extra-3"), Extra("extra-4"), Extra("extra-1"));
        var four = WithExtras(Extra("extra-1"), Extra("extra-2"), Extra("extra-3"), Extra("extra-4"));

        ExtraColours.Check(five).ShouldBe([new ManifestFinding("too_many_extras", "palette.extras", "A palette can hold at most 4 extra colours.")]);
        ExtraColours.Check(four).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenThousandsOfExtras_ShouldAnswerWithOneFinding()
    {
        var many = WithExtras(Enumerable.Range(0, 5000).Select(_ => Extra("</script>", "x", "y")).ToArray());

        ExtraColours.Check(many).Select(finding => finding.Code).ShouldBe(["too_many_extras"]);
    }

    [Test]
    public void Check_WhenAnExtraIsMalformed_ShouldNameEachFieldAndNeverEchoIt()
    {
        var findings = ExtraColours.Check(WithExtras(Extra("extra-5<script>", "#fff", "url(x)")));

        findings.Select(finding => (finding.Code, finding.Path)).ShouldBe(
        [
            ("manifest_invalid", "palette.extras[0].name"),
            ("manifest_invalid", "palette.extras[0].colour"),
            ("manifest_invalid", "palette.extras[0].ink"),
        ]);
        findings.ShouldAllBe(finding => !finding.Message.Contains("script") && !finding.Message.Contains("url"));
    }

    [Test]
    public void ExtraNames_WhenListed_ShouldBeOnePerAllowedExtra()
    {
        ExtraColours.Names.ShouldBe(["extra-1", "extra-2", "extra-3", "extra-4"]);
        ExtraColours.Names.Count.ShouldBe(TemplateLimits.MAX_EXTRAS);
    }
}
