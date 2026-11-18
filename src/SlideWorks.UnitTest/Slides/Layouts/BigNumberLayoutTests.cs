using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class BigNumberLayoutTests
{
    [Test]
    public void Validate_WhenTheNumberPartIs8_ShouldRefuse()
    {
        Validate(Slots("12345678")).Errors.ShouldBe(["big-number.value: the number part is at most 6 characters (got 8); put the rest in the caption"]);
    }

    [Test]
    public void Validate_WhenTheNumberPartIs6AndTheUnitIs4_ShouldAccept()
    {
        Validate(Slots("999999 wks")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenValueIs11_ShouldRefuse()
    {
        Validate(Slots("999999 weeks")).Errors.ShouldBe(["big-number.value: at most 10 characters (got 12)"]);
        Validate(Slots("12345678901")).Errors.ShouldBe(["big-number.value: at most 10 characters (got 11)"]);
    }

    [Test]
    public void Validate_WhenTheValueHasNoDigit_ShouldCountItAllAsTheNumberPart()
    {
        Validate(Slots("sevenfold")).Errors.ShouldBe(["big-number.value: the number part is at most 6 characters (got 9); put the rest in the caption"]);
    }

    [Test]
    public void Validate_WhenTheNumberHasASignDotsAndAUnit_ShouldSplitLikeTheClient()
    {
        Validate(Slots("$1,234.5M")).Errors.ShouldBe(["big-number.value: the number part is at most 6 characters (got 8); put the rest in the caption"]);
        Validate(Slots("$4.2M")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenDetailIsMissing_ShouldBeZero()
    {
        Count("""{ "headline": "h", "value": "1", "caption": "c", "reveal": "one-by-one" }""").ShouldBe(0);
    }

    [Test]
    public void Count_WhenDetailAndOneByOne_ShouldBeOne()
    {
        Count(Slots("1", "\"reveal\": \"one-by-one\"")).ShouldBe(1);
    }

    [Test]
    public void Count_WhenDetailAndRevealIsAll_ShouldBeZero()
    {
        Count(Slots("1", "\"reveal\": \"all\"")).ShouldBe(0);
        Count(Slots("1")).ShouldBe(0);
    }

    [Test]
    public void Count_WhenTheCalloutRevealsLast_ShouldAddOne()
    {
        Count(Slots("1", "\"reveal\": \"one-by-one\", \"callout\": { \"tone\": \"info\", \"text\": \"c\", \"reveal\": \"last\" }")).ShouldBe(2);
    }

    [Test]
    public void Validate_WhenEmphasisNamesTheCaption_ShouldAccept()
    {
        Validate(Slots("1", "\"emphasis\": [{ \"text\": \"weeks\", \"tone\": \"accent\", \"in\": \"caption\" }]")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenThePhraseIsInBothTargetsWithoutIn_ShouldRefuse()
    {
        var slots = """{ "headline": "Median weeks", "value": "1", "caption": "Median weeks", "emphasis": [{ "text": "weeks", "tone": "accent" }] }""";

        Validate(slots).Errors.ShouldBe(["big-number.emphasis[0].text: 'weeks' is in headline and caption; say which with 'in'"]);
    }

    [Test]
    public void Validate_WhenACalloutIsSet_ShouldAccept()
    {
        Validate(Slots("1", "\"callout\": { \"tone\": \"info\", \"text\": \"A callout under the number.\" }")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Definition_WhenRead_ShouldAllowEitherSurface()
    {
        BigNumberLayout.Definition.Surface.ShouldBe(LayoutSurface.Either);
    }

    private static string Slots(string value, string? extra = null) =>
        $$"""{ "headline": "Lead time", "value": "{{value}}", "caption": "Median weeks", "detail": "Eight of twelve epics"{{(extra is null ? "" : ", " + extra)}} }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(BigNumberLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(BigNumberLayout.Definition, document.RootElement);
    }
}
