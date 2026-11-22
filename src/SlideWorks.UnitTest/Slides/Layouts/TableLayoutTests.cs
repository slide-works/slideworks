using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class TableLayoutTests
{
    [Test]
    public void Validate_WhenStrikeAndAnswerHasThreeColumns_ShouldRefuse()
    {
        Validate("""{ "headline": "h", "columns": ["a", "b", "c"], "rows": [["1", "2", "3"]], "reveal": "strike-and-answer" }""").Errors
            .ShouldContain("table.reveal: 'strike-and-answer' needs exactly 2 columns (got 3)");
    }

    [Test]
    public void Validate_WhenStrikeAndAnswerHasTwoColumns_ShouldAccept()
    {
        Validate("""{ "headline": "h", "columns": ["a", "b"], "rows": [["1", "2"]], "reveal": "strike-and-answer" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        Validate("""{ "headline": "Small steps", "columns": ["a", "b"], "rows": [["1", "2"]], "emphasis": [{ "text": "big", "tone": "accent" }] }""").Errors
            .ShouldContain(e => e.StartsWith("table.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Count_WhenOneByOne_ShouldBeRows()
    {
        Count("""{ "headline": "h", "columns": ["a", "b"], "rows": [["1", "2"], ["3", "4"], ["5", "6"]], "reveal": "one-by-one" }""").ShouldBe(3);
    }

    [Test]
    public void Count_WhenRevealIsAll_ShouldBeZero()
    {
        Count("""{ "headline": "h", "columns": ["a", "b"], "rows": [["1", "2"], ["3", "4"]] }""").ShouldBe(0);
    }

    [Test]
    public void Count_WhenStrikeAndAnswer_ShouldBeRows()
    {
        Count("""{ "headline": "h", "columns": ["a", "b"], "rows": [["1", "2"], ["3", "4"]], "reveal": "strike-and-answer" }""").ShouldBe(2);
    }

    [Test]
    public void Count_WhenStrikeAndAnswerAndCalloutRevealsLast_ShouldBeRowsPlusOne()
    {
        Count("""{ "headline": "h", "columns": ["a", "b"], "rows": [["1", "2"], ["3", "4"]], "reveal": "strike-and-answer", "callout": { "tone": "accent", "text": "c", "reveal": "last" } }""").ShouldBe(3);
    }

    [Test]
    public void Validate_WhenACellIs80_ShouldAccept()
    {
        Validate(Cells(80)).Errors.ShouldBeEmpty();
    }

    // SW-767: four rows of two 80-character cells in strike-and-answer is the long-cell shape that 100 and 120 did not fit.
    [Test]
    public void Validate_WhenFourRowsOfTwoCellsAre80_ShouldAcceptStrikeAndAnswer()
    {
        var cell = new string('a', 80);
        var row = $$"""["{{cell}}", "{{cell}}"]""";
        Validate($$"""{ "headline": "h", "columns": ["a", "b"], "rows": [{{row}}, {{row}}, {{row}}, {{row}}], "reveal": "strike-and-answer" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenACellIs81_ShouldRefuse()
    {
        Validate(Cells(81)).Errors.ShouldContain(e => e.StartsWith("table.rows[0][0]", StringComparison.Ordinal) && e.Contains("80", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheCalloutPushesTheRowsPastTheBody_ShouldRefuse()
    {
        // Six one-line rows are 61 + 6 * 80.1 = 541.6 px; a one-line callout is 123.8 more.
        const string ROWS = """[["1", "2"], ["1", "2"], ["1", "2"], ["1", "2"], ["1", "2"], ["1", "2"]]""";

        Validate($$"""{ "headline": "h", "columns": ["a", "b"], "rows": {{ROWS}} }""").Errors.ShouldBeEmpty();
        Validate($$"""{ "headline": "h", "columns": ["a", "b"], "rows": {{ROWS}}, "callout": { "tone": "accent", "text": "c" } }""").Errors
            .ShouldHaveSingleItem().ShouldStartWith("table.rows: too much text to fit");
    }

    private static string Cells(int length) =>
        $$"""{ "headline": "h", "columns": ["a", "b"], "rows": [["{{new string('a', length)}}", "2"]] }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(TableLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(TableLayout.Definition, document.RootElement);
    }
}
