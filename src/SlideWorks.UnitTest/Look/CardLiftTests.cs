using SlideWorks.Core.Look;
using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.UnitTest.Look;

// SW-148: a card on dark paper gets the dark lift instead of the template's shadow; a flat template stays flat.
public class CardLiftTests
{
    private const string TEMPLATE_SHADOW = "0 1px 2px rgb(15 23 42 / 0.06), 0 14px 36px rgb(15 23 42 / 0.09)";

    [TestCase("#0e1424")]
    [TestCase("#05070d")]
    [TestCase("rgb(20 20 20)")]
    public void For_WhenThePaperIsDark_ShouldBeTheDarkLift(string paper)
    {
        CardLift.For(TEMPLATE_SHADOW, paper).ShouldBe(CardLift.DARK);
    }

    [TestCase("#f7f8fa")]
    [TestCase("#ffffff")]
    [TestCase("#808080")]
    public void For_WhenThePaperIsLight_ShouldBeTheTemplatesShadow(string paper)
    {
        CardLift.For(TEMPLATE_SHADOW, paper).ShouldBe(TEMPLATE_SHADOW);
    }

    [Test]
    public void For_WhenTheTemplateIsFlat_ShouldStayFlatOnDarkPaper()
    {
        CardLift.For("none", "#0e1424").ShouldBe("none");
    }

    [TestCase(null)]
    [TestCase("not a colour")]
    public void For_WhenThePaperIsNotAColour_ShouldBeTheTemplatesShadow(string? paper)
    {
        CardLift.For(TEMPLATE_SHADOW, paper).ShouldBe(TEMPLATE_SHADOW);
    }

    [Test]
    public void Dark_ShouldPassTheManifestShadowCheck()
    {
        ManifestShadow.IsSafe(CardLift.DARK).ShouldBeTrue();
    }

    [Test]
    public void For_WhenTheMidnightPalette_ShouldBeTheDarkLiftAndCobaltShouldNot()
    {
        var palettes = new PaletteCatalogue();

        CardLift.For(TEMPLATE_SHADOW, palettes.Find("midnight")!.Tokens["paper"]).ShouldBe(CardLift.DARK);
        CardLift.For(TEMPLATE_SHADOW, palettes.Find("cobalt")!.Tokens["paper"]).ShouldBe(TEMPLATE_SHADOW);
    }
}
