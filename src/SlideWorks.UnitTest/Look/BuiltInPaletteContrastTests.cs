using SlideWorks.Core.Look;

namespace SlideWorks.UnitTest.Look;

// L2: every built-in palette passes every contrast pair, so a palette that drops below a bar fails the build.
public class BuiltInPaletteContrastTests
{
    private static readonly string[] _pairs =
    [
        "body", "secondary", "card-bold", "headline-accent", "on-accent", "deep-dive-pill", "panel-accent",
        "panel-positive", "panel-negative", "panel-inverse", "badge-positive", "badge-negative", "dark-subtitle",
        "soft-accent", "soft-inverse", "deep-dive-list",
    ];

    private static IEnumerable<string> PaletteNames() => new PaletteCatalogue().All.Select(p => p.Name);

    [TestCaseSource(nameof(PaletteNames))]
    public void EveryPairMeetsItsBar_WhenBuiltInPalette_ShouldPass(string name)
    {
        var checks = PaletteContrast.Check(new PaletteCatalogue().Find(name)!);

        checks.Select(c => c.Pair).ShouldBe(_pairs);
        checks.Where(c => !c.Pass)
            .Select(c => $"{c.Pair}: {c.Fg} on {c.Bg} is {c.Ratio:0.00}:1, below {c.Bar}:1")
            .ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenAPairIsBelowItsBar_ShouldFailThatPairOnly()
    {
        // A made-up palette: Cobalt with a pale grey secondary text colour (#b0b0b0 on #f7f8fa is about 2.1:1).
        var cobalt = new PaletteCatalogue().Find("cobalt")!;
        var tokens = new Dictionary<string, string>(cobalt.Tokens) { ["muted"] = "#b0b0b0" };
        var faded = cobalt with { Name = "faded", Tokens = tokens };

        var failing = PaletteContrast.Check(faded).Where(c => !c.Pass).ToList();

        failing.Select(c => c.Pair).ShouldBe(["secondary"]);
        failing[0].Ratio.ShouldBeLessThan(failing[0].Bar);
    }

    // The screen JSON's contrast.lowest examples (SW-60 D9): the pair nearest to failing, the smallest
    // ratio divided by its bar, with the ratio rounded to one decimal.
    [TestCase("cobalt", "panel-positive", 5.0, 4.5)]
    [TestCase("ember", "on-accent", 5.2, 4.5)]
    public void Check_WhenLowestPairByRatioOverBar_ShouldMatchTheScreenJsonExamples(string name, string pair, double ratio, double bar)
    {
        var lowest = PaletteContrast.Check(new PaletteCatalogue().Find(name)!).MinBy(c => c.Ratio / c.Bar)!;

        (lowest.Pair, Math.Round(lowest.Ratio, 1), lowest.Bar).ShouldBe((pair, ratio, bar));
    }

    [Test]
    public void Ratio_WhenBlackOnWhite_ShouldBeTwentyOne()
    {
        Contrast.Ratio("#000", "rgb(255 255 255)").ShouldBe(21, 0.0001);
        Contrast.Ratio("#2f5bea", "#2f5bea").ShouldBe(1, 0.0001);
    }

    [Test]
    public void Ratio_WhenWhiteOnPlainPositive_ShouldMatchTheDescription()
    {
        // SW-2's contrast section: white on Cobalt's positive colour is 3.3:1, which is why panels have their own ink.
        var cobalt = new PaletteCatalogue().Find("cobalt")!;

        Contrast.Ratio("#ffffff", cobalt.Tokens["good"]).ShouldBe(3.3, 0.05);
    }
}
