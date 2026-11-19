using SlideWorks.Core.Look;

namespace SlideWorks.UnitTest.Look;

[TestFixture]
public class PaletteTokensTests
{
    [Test]
    public void Apply_WhenATokenIsReplaced_ShouldKeepThePaletteNameAndTheOtherTokens()
    {
        var palette = new PaletteCatalogue().Default;
        var applied = PaletteTokens.Apply(palette, """{"accent":"#123456"}""");

        applied.Name.ShouldBe(palette.Name);
        applied.Tokens["accent"].ShouldBe("#123456");
        applied.Tokens["ink"].ShouldBe(palette.Tokens["ink"]);
    }

    [Test]
    public void Check_WhenTheColourFailsContrast_ShouldRefuseIt()
    {
        var palette = new PaletteCatalogue().Default;

        var refused = PaletteTokens.Check(palette, new Dictionary<string, string> { ["ink"] = "#ffffff" });

        refused.ShouldNotBeNull();
        refused.ShouldContain("colors:");
    }

    [Test]
    public void Check_WhenTheTokenIsUnknown_ShouldNameIt()
    {
        var palette = new PaletteCatalogue().Default;

        PaletteTokens.Check(palette, new Dictionary<string, string> { ["nope"] = "#123456" }).ShouldBe("colors.nope: unknown colour");
    }
}
