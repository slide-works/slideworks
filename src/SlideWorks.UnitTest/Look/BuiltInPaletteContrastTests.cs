using SlideWorks.Core.Look;

namespace SlideWorks.UnitTest.Look;

// L2: every built-in palette passes every contrast pair, so a palette that drops below a bar fails the build.
public class BuiltInPaletteContrastTests
{
    private static readonly string[] _pairs =
    [
        "body", "secondary", "card-bold", "headline-accent", "on-accent", "deep-dive-pill", "panel-accent",
        "panel-positive", "panel-negative", "panel-inverse", "badge-positive", "badge-negative", "dark-subtitle",
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
