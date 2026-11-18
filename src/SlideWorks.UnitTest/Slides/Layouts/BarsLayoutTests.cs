using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class BarsLayoutTests
{
    [Test]
    public void Validate_WhenWasLabelHasNoWas_ShouldRefuse()
    {
        Validate(Slots("""[{ "label": "a", "value": 1 }, { "label": "b", "value": 2 }]""", "\"wasLabel\": \"Before\", ")).Errors.ShouldHaveSingleItem()
            .ShouldBe("bars.wasLabel: needs a bar with a was to explain; give a bar a was, or drop the wasLabel");
    }

    [Test]
    public void Validate_WhenWasLabelHasAWas_ShouldAccept()
    {
        Validate(Slots("""[{ "label": "a", "value": 1, "was": 2 }, { "label": "b", "value": 2 }]""", "\"wasLabel\": \"Before\", ")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenEveryValueIsZero_ShouldRefuse()
    {
        Validate(Slots("""[{ "label": "a", "value": 0 }, { "label": "b", "value": 0, "was": 0 }]""")).Errors.ShouldHaveSingleItem()
            .ShouldBe("bars.bars: needs at least one value or was above 0, or there is nothing to draw to scale");
    }

    [Test]
    public void Validate_WhenOnlyAWasIsAboveZero_ShouldAccept()
    {
        Validate(Slots("""[{ "label": "a", "value": 0, "was": 3 }, { "label": "b", "value": 0 }]""")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenANoteFitsNeitherInsideNorAfter_ShouldRefuse()
    {
        // The short bar is 6 px, so its 50-character note (616 px) goes after it, and then its own end label
        // (a 20-character value and unit, and a tag: 636 px) leaves no room in the 1248 px track.
        var note = new string('n', 50);
        var bars = $$"""[{ "label": "a", "value": 1000, "tag": "Sixteen chars!!!" }, { "label": "b", "value": 0.123456789, "note": "{{note}}", "tag": "Sixteen chars!!!" }]""";

        Validate(Slots(bars, "\"unit\": \"eightchr\", ")).Errors.ShouldHaveSingleItem()
            .ShouldBe("bars.bars[1].note: too much text to fit in this bar; shorten it or leave it out");
    }

    [Test]
    public void Validate_WhenANoteFitsAfterAShortBar_ShouldAccept()
    {
        var bars = """[{ "label": "a", "value": 100 }, { "label": "b", "value": 10, "note": "A short note" }]""";

        Validate(Slots(bars)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenSixBarsPassTheBody_ShouldRefuse()
    {
        var bar = $$"""{ "label": "{{new string('a', 15)}} {{new string('b', 14)}}", "sub": "{{new string('c', 20)}} {{new string('d', 19)}}", "value": 3 }""";
        var bars = "[" + string.Join(", ", Enumerable.Repeat(bar, 6)) + "]";

        Validate(Slots(bars)).Errors.ShouldHaveSingleItem().ShouldStartWith("bars.bars: too much text to fit");
    }

    [Test]
    public void Validate_WhenOneBar_ShouldRefuse()
    {
        Validate(Slots("""[{ "label": "a", "value": 1 }]""")).Errors.ShouldContain(e => e.StartsWith("bars.bars", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        Validate(Slots("""[{ "label": "a", "value": 1 }, { "label": "b", "value": 2 }]""", "\"emphasis\": [{ \"text\": \"big\", \"tone\": \"accent\" }], ")).Errors
            .ShouldContain(e => e.StartsWith("bars.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Count_WhenOneByOne_ShouldBeBars()
    {
        Count(Slots("""[{ "label": "a", "value": 1 }, { "label": "b", "value": 2 }, { "label": "c", "value": 3 }]""", "\"reveal\": \"one-by-one\", ")).ShouldBe(3);
    }

    [Test]
    public void Count_WhenRevealIsAll_ShouldBeZero()
    {
        Count(Slots("""[{ "label": "a", "value": 1 }, { "label": "b", "value": 2 }]""")).ShouldBe(0);
    }

    [Test]
    public void Count_WhenOneByOneAndCalloutRevealsLast_ShouldBeBarsPlusOne()
    {
        Count(Slots("""[{ "label": "a", "value": 1 }, { "label": "b", "value": 2 }]""", "\"reveal\": \"one-by-one\", \"callout\": { \"tone\": \"accent\", \"text\": \"c\", \"reveal\": \"last\" }, ")).ShouldBe(3);
    }

    private static string Slots(string bars, string extra = "") => $$"""{ "headline": "Small steps", {{extra}} "bars": {{bars}} }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(BarsLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(BarsLayout.Definition, document.RootElement);
    }
}
