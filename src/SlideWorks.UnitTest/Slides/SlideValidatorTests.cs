using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class SlideValidatorTests
{
    private static IEnumerable<TestCaseData> SampleSlides() =>
        Enumerable.Range(1, 16).Select(n => new TestCaseData(n).SetArgDisplayNames(n.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [TestCaseSource(nameof(SampleSlides))]
    public void Validate_WhenSampleDeckSlide_ShouldPass(int number)
    {
        var slide = SampleDeck.Slide(number);

        var result = SlideValidator.Validate(slide.GetProperty("layout").GetString()!, slide.GetProperty("slots"));

        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenSampleDeckImageSlide_ShouldReturnItsImageId()
    {
        var slide = SampleDeck.Slide(10);

        var result = SlideValidator.Validate("two-column", slide.GetProperty("slots"));

        result.ImageIds.ShouldBe(["img_sample0001"]);
    }

    [Test]
    public void Validate_WhenRequiredSlotMissing_ShouldNameThePath()
    {
        var result = Validate("practice", """
            { "headline": "Slice it", "do": [{ "lead": "a", "text": "b" }, { "lead": "c", "text": "d" }, { "text": "e" }],
              "comes_back": [], "avoid": [] }
            """);

        result.Error.ShouldBe("practice.do[2].lead: required");
    }

    [Test]
    public void Validate_WhenRequiredTopLevelSlotMissing_ShouldNameIt()
    {
        var result = Validate("quote", """{ "quote": "Ship it." }""");

        result.Error.ShouldBe("quote.attribution: required");
    }

    [Test]
    public void Validate_WhenUnknownField_ShouldNameIt()
    {
        var result = Validate("practice", """
            { "headline": "Slice it", "do": [{ "lead": "a", "text": "b" }], "comes_back": [], "avoid": [], "foo": 1 }
            """);

        result.Error.ShouldBe("practice.foo: unknown field");
    }

    [Test]
    public void Validate_WhenTooManyItems_ShouldNameTheLimit()
    {
        var items = string.Join(", ", Enumerable.Range(1, 9).Select(i => $"\"Point {i}\""));

        var result = Validate("bullets", $$"""{ "headline": "Nine points", "items": [{{items}}] }""");

        result.Error.ShouldBe("bullets.items: at most 8 items");
    }

    [Test]
    public void Validate_WhenTooFewItems_ShouldNameTheLimit()
    {
        var result = Validate("numbered-cards", """{ "headline": "One card", "cards": [{ "title": "a", "text": "b" }] }""");

        result.Error.ShouldBe("numbered-cards.cards: at least 2 items");
    }

    [Test]
    public void Validate_WhenTooLong_ShouldNameTheLimit()
    {
        var result = Validate("big-number", """{ "headline": "Lead time", "value": "eleven weeks", "caption": "Median" }""");

        result.Error.ShouldBe("big-number.value: at most 8 characters (got 12)");
    }

    [Test]
    public void Validate_WhenUnknownLayout_ShouldListKnownOnes()
    {
        var result = Validate("foo", "{}");

        result.Error.ShouldBe("layout: unknown layout 'foo' (known: title, divider, bullets, two-column, practice, compare, "
            + "big-number, quote, image, table, chart, gantt, numbered-cards, rules, cards, close)");
    }

    [TestCase("""{ "headline": 5, "items": ["a"] }""", "bullets.headline: expected text")]
    [TestCase("""{ "headline": "h", "items": "a" }""", "bullets.items: expected a list")]
    [TestCase("""{ "headline": "h", "items": [true] }""", "bullets.items[0]: expected text or an object")]
    [TestCase("""{ "headline": "h", "items": ["a"], "reveal": "some" }""", "bullets.reveal: expected one of 'all', 'one-by-one' (got 'some')")]
    [TestCase("""{ "headline": "h", "items": ["a"], "callout": "careful" }""", "bullets.callout: expected an object")]
    public void Validate_WhenWrongKind_ShouldNameExpectedKind(string slots, string error)
    {
        Validate("bullets", slots).Error.ShouldBe(error);
    }

    [Test]
    public void Validate_WhenSlotsNotAnObject_ShouldSayExpectedObject()
    {
        Validate("quote", "[]").Error.ShouldBe("quote: expected an object");
    }

    [Test]
    public void Validate_WhenOptionalSlotIsNull_ShouldTreatItAsAbsent()
    {
        Validate("quote", """{ "kicker": null, "quote": "Ship it.", "attribution": "Sam" }""").IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenTextIsEmpty_ShouldSayNotEmpty()
    {
        Validate("quote", """{ "quote": " ", "attribution": "Sam" }""").Error.ShouldBe("quote.quote: must not be empty");
    }

    [Test]
    public void Validate_WhenLineBreakOutsideCloseHeadline_ShouldRefuseIt()
    {
        Validate("quote", """{ "quote": "one\ntwo", "attribution": "Sam" }""").Error.ShouldBe("quote.quote: line breaks are not allowed here");
    }

    [Test]
    public void Validate_WhenCloseHeadlineHasFourLines_ShouldNameTheLimit()
    {
        Validate("close", """{ "headline": "a\nb\nc\nd" }""").Error.ShouldBe("close.headline: at most 3 lines (got 4)");
    }

    [Test]
    public void Validate_WhenTwoColumnSideHasTextAndImage_ShouldRequireExactlyOne()
    {
        var result = Validate("two-column", """
            { "headline": "h", "left": { "text": "a" }, "right": { "text": "b", "image": { "image": "img_1", "alt": "x" } } }
            """);

        result.Error.ShouldBe("two-column.right: exactly one of items, text, image");
    }

    [Test]
    public void Validate_WhenImageIdHasSlash_ShouldRefuseIt()
    {
        var result = Validate("image", """{ "headline": "h", "image": { "image": "../etc", "alt": "x" } }""");

        result.Error.ShouldBe("image.image.image: not an image id (use the id upload_image returned)");
        result.ImageIds.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTableRowHasTooFewCells_ShouldNameTheRow()
    {
        var result = Validate("table", """
            { "headline": "h", "columns": ["a", "b", "c"], "rows": [["1", "2", "3"], ["1", "2"]] }
            """);

        result.Error.ShouldBe("table.rows[1]: 2 cells, but there are 3 columns");
    }

    [Test]
    public void Validate_WhenChartSeriesHasTooFewValues_ShouldNameTheSeries()
    {
        var result = Validate("chart", """
            { "headline": "h", "kind": "line", "xLabels": ["a", "b", "c"], "series": [{ "name": "s", "values": [1, 2] }] }
            """);

        result.Error.ShouldBe("chart.series[0].values: 2 values, but there are 3 xLabels");
    }

    [Test]
    public void Validate_WhenGanttBarEndsPastLastColumn_ShouldNameTheRow()
    {
        var result = Validate("gantt", """
            { "headline": "h", "columns": ["a", "b"], "rows": [{ "label": "x", "start": 1, "end": 3, "status": "done" }] }
            """);

        result.Error.ShouldBe("gantt.rows[0].end: at most 2 (the number of columns)");
    }

    [Test]
    public void Validate_WhenGanttStartIsFractional_ShouldExpectWholeNumber()
    {
        var result = Validate("gantt", """
            { "headline": "h", "columns": ["a", "b"], "rows": [{ "label": "x", "start": 1.5, "end": 2, "status": "done" }] }
            """);

        result.Error.ShouldBe("gantt.rows[0].start: expected a whole number");
    }

    [Test]
    public void Validate_WhenSeveralErrors_ShouldReportEach()
    {
        var result = Validate("quote", """{ "foo": 1 }""");

        result.Errors.ShouldBe(["quote.foo: unknown field", "quote.quote: required", "quote.attribution: required"]);
    }

    private static SlideValidationResult Validate(string layout, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(layout, document.RootElement);
    }
}
