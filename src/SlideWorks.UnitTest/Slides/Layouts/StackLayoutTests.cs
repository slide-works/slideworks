using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class StackLayoutTests
{
    [Test]
    public void Validate_WhenOnlySomeSegmentsHaveValues_ShouldRefuse()
    {
        var segments = """[{ "label": "a", "value": 1 }, { "label": "b", "value": 2 }, { "label": "c", "value": 3 }, { "label": "d" }]""";

        Validate(Slots(segments)).Errors.ShouldContain("stack.segments[3].value: required, because other segments have a value");
    }

    [Test]
    public void Validate_WhenEverySegmentHasAValue_ShouldAccept()
    {
        Validate(Slots("""[{ "label": "a", "value": 1.5 }, { "label": "b", "value": 2 }]""", "\"unit\": \"ms\", ")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenUnitHasNoValues_ShouldRefuse()
    {
        Validate(Slots("""[{ "label": "a" }, { "label": "b" }]""", "\"unit\": \"ms\", ")).Errors.ShouldContain(
            "stack.unit: not allowed without values: give every segment a value, or drop the unit");
    }

    [Test]
    public void Validate_WhenAValueIsZero_ShouldRefuse()
    {
        Validate(Slots("""[{ "label": "a", "value": 0 }, { "label": "b", "value": 2 }]""")).Errors.ShouldContain("stack.segments[0].value: must be above 0");
    }

    [Test]
    public void Validate_WhenTheRowsPassTheStack_ShouldRefuse()
    {
        // 33 characters fit a line, so a 40-character label of two words takes two lines of 33.8 px; with a note a row is 103.6 px.
        var row = $$"""{ "label": "{{new string('a', 20)}} {{new string('b', 19)}}", "note": "n" }""";
        var segments = "[" + string.Join(", ", Enumerable.Repeat(row, 5)) + "]";

        Validate(Slots(segments)).Errors.ShouldHaveSingleItem().ShouldStartWith("stack.segments: too much text to fit");
    }

    [Test]
    public void Validate_WhenThePointsAndCalloutPassTheBody_ShouldRefuse()
    {
        var point = $$"""{ "lead": "Lead", "text": "{{new string('a', 25)}} {{new string('b', 25)}} {{new string('c', 25)}}" }""";
        var points = "[" + string.Join(", ", Enumerable.Repeat(point, 4)) + "]";
        var callout = $$"""{ "tone": "accent", "text": "{{new string('d', 60)}}" }""";

        Validate(Slots("""[{ "label": "a" }, { "label": "b" }]""", $"\"points\": {points}, \"callout\": {callout}, ")).Errors
            .ShouldHaveSingleItem().ShouldStartWith("stack.points: too much text to fit");
    }

    [Test]
    public void Validate_WhenOneSegment_ShouldRefuse()
    {
        Validate(Slots("""[{ "label": "a" }]""")).Errors.ShouldContain(e => e.StartsWith("stack.segments", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        Validate(Slots("""[{ "label": "a" }, { "label": "b" }]""", "\"emphasis\": [{ \"text\": \"big\", \"tone\": \"accent\" }], ")).Errors
            .ShouldContain(e => e.StartsWith("stack.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Count_WhenFiveSegments_ShouldBeFive()
    {
        Count(Slots("""[{ "label": "a" }, { "label": "b" }, { "label": "c" }, { "label": "d" }, { "label": "e" }]""", "\"reveal\": \"one-by-one\", ")).ShouldBe(5);
    }

    [Test]
    public void Count_WhenRevealIsAll_ShouldBeZero()
    {
        Count(Slots("""[{ "label": "a" }, { "label": "b" }]""")).ShouldBe(0);
    }

    [Test]
    public void Count_WhenOneByOneAndCalloutRevealsLast_ShouldBeSegmentsPlusOne()
    {
        Count(Slots("""[{ "label": "a" }, { "label": "b" }]""", "\"reveal\": \"one-by-one\", \"callout\": { \"tone\": \"accent\", \"text\": \"c\", \"reveal\": \"last\" }, ")).ShouldBe(3);
    }

    private static string Slots(string segments, string extra = "") => $$"""{ "headline": "Small steps", {{extra}} "segments": {{segments}} }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(StackLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(StackLayout.Definition, document.RootElement);
    }
}
