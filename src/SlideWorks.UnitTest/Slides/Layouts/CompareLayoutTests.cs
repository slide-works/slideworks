using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

// The snippets below write JSON with single quotes; Compare turns them into double quotes.
[TestFixture]
public class CompareLayoutTests
{
    private const string POINTS = "'points': ['p']";
    private const string ROWS = "'rows': [{ 'label': 'l', 'value': 'v' }]";
    private const string CALLOUT = ", 'callout': { 'tone': 'accent', 'text': 'c' }";
    private const string ONE_BY_ONE = ", 'reveal': 'one-by-one'";
    private const string STAMPED = "'points': ['p'], 'stamp': 'S'";

    [Test]
    public void Validate_WhenAPanelHasPointsAndRows_ShouldRefuse()
    {
        Validate(Compare($"{POINTS}, {ROWS}")).Errors.ShouldContain("compare.failure: exactly one of points, rows, line");
    }

    [Test]
    public void Validate_WhenAPanelHasNone_ShouldRefuse()
    {
        Validate(Compare("'sub': 's'")).Errors.ShouldContain("compare.failure: exactly one of points, rows, line");
    }

    [Test]
    public void Validate_WhenRowsPassTheBody_ShouldRefuse()
    {
        // 160 + 48.4 + four rows of (25 + 2 * 38.4) = 616 px fits the 620 px body; a stamp (78) takes it to
        // 694 px.
        var value = $"{new string('a', 20)} {new string('b', 15)}";
        var rows = string.Join(", ", Enumerable.Repeat($"{{ 'label': 'l', 'value': '{value}' }}", 4));
        var panel = $"'rows': [{rows}]";

        Validate(Compare(panel)).Errors.ShouldBeEmpty();
        Validate(Compare(panel + ", 'stamp': 'S'")).Errors.ShouldHaveSingleItem()
            .ShouldBe("compare.failure: too much text to fit on the slide (about 694 px of 620 px); use fewer or shorter items");
    }

    [Test]
    public void Validate_WhenTitleIs51_ShouldRefuse()
    {
        Validate(Compare(POINTS, new string('t', 51))).Errors
            .ShouldContain(e => e.StartsWith("compare.failure.title", StringComparison.Ordinal) && e.Contains("50", StringComparison.Ordinal));
        Validate(Compare(POINTS, new string('t', 50))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenEmphasisNamesTheFailureLine_ShouldAccept()
    {
        var slots = Compare("'line': 'A Quoted line'", extra: ", 'emphasis': [{ 'text': 'Quoted', 'tone': 'accent' }]");

        Validate(slots).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheCalloutPushesAPanelPastTheBody_ShouldRefuse()
    {
        // 160 + 48.4 + 4 * 37.8 + 3 * 16 + 78 (stamp) + 68.6 (footer) = 554 px: it fits 620 px, not the
        // 496.2 px a one-line callout leaves.
        const string PANEL = "'points': ['p', 'p', 'p', 'p'], 'stamp': 'S', 'footer': 'f'";

        Validate(Compare(PANEL)).Errors.ShouldBeEmpty();
        Validate(Compare(PANEL, extra: CALLOUT)).Errors.ShouldHaveSingleItem()
            .ShouldBe("compare.failure: too much text to fit on the slide (about 554 px of 496.2 px); use fewer or shorter items");
    }

    [Test]
    public void Validate_WhenTheIconIsUnknown_ShouldRefuse()
    {
        Validate(Compare($"{POINTS}, 'icon': 'zzzzzzzzzz'")).Errors
            .ShouldContain(e => e.StartsWith("compare.failure.icon: unknown icon 'zzzzzzzzzz'", StringComparison.Ordinal));
    }

    [Test]
    public void Count_WhenBothStampsAndOneByOne_ShouldBeTwo()
    {
        Count(Compare(STAMPED, extra: ONE_BY_ONE, success: STAMPED)).ShouldBe(2);
    }

    [Test]
    public void Count_WhenNoStampAndOneByOne_ShouldBeOne()
    {
        Count(Compare(POINTS, extra: ONE_BY_ONE)).ShouldBe(1);
    }

    [Test]
    public void Count_WhenOneStampAndRevealIsAll_ShouldBeZero()
    {
        Count(Compare(STAMPED)).ShouldBe(0);
    }

    private static string Compare(string failure, string title = "t", string extra = "", string success = POINTS) =>
        $"{{ 'headline': 'Headline', 'failure': {{ 'label': 'l', 'title': '{title}', {failure} }}, 'success': {{ 'label': 'l', 'title': 't', {success} }}{extra} }}"
            .Replace('\'', '"');

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(CompareLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(CompareLayout.Definition, document.RootElement);
    }
}
