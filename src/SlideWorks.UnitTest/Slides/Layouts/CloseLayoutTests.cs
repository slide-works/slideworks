using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class CloseLayoutTests
{
    [Test]
    public void Validate_WhenEmphasisNamesTheSub_ShouldAccept()
    {
        Validate("""{ "headline": "Ship it", "sub": "Then learn what happened", "emphasis": [{ "text": "learn", "tone": "accent", "in": "sub" }] }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenEmphasisNamesASubThatIsNotSet_ShouldRefuse()
    {
        Validate("""{ "headline": "Ship it", "emphasis": [{ "text": "Ship", "tone": "accent", "in": "sub" }] }""").Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Validate_WhenCloseHasACallout_ShouldRefuse()
    {
        Validate("""{ "headline": "Ship it", "callout": { "tone": "info", "text": "c" } }""").Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Validate_WhenAPhraseSpansALineBreak_ShouldRefuse()
    {
        Validate("""{ "headline": "Ship it\nthis week", "emphasis": [{ "text": "it\nthis", "tone": "accent" }] }""").Errors.ShouldBe(["close.emphasis[0].text: line breaks are not allowed here"]);
    }

    // A four-line headline, a sub of capital W that wraps to three lines and an author that wraps to two.
    private const string FOUR_LINE_HEADLINE = "Small Batches Show Us What Works Soon\\nHalf Every Week Now\\nLearn From Real Use";

    [Test]
    public void Validate_WhenTheStackIsTooTall_ShouldRefuseAsTooMuchText()
    {
        var sub = string.Join(" ", Enumerable.Repeat("WWWWWWWWW", 8));
        var author = string.Join(" ", Enumerable.Repeat("WWWWWWWWW", 6));

        Validate($$"""{ "headline": "{{FOUR_LINE_HEADLINE}}", "sub": "{{sub}}", "author": "{{author}}" }""").Errors.ShouldBe(
            ["close.sub: too much text to fit: the headline, sub and author take about 856 px, and at most 846 px fit; shorten the sub or the author"]);
    }

    [Test]
    public void Validate_WhenTheAuthorTakesOneLine_ShouldAcceptTheSameSub()
    {
        var sub = string.Join(" ", Enumerable.Repeat("WWWWWWWWW", 8));

        Validate($$"""{ "headline": "{{FOUR_LINE_HEADLINE}}", "sub": "{{sub}}", "author": "Sam Rivera" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenCloseHasEmphasis_ShouldBeZero()
    {
        Count("""{ "headline": "Ship it", "emphasis": [{ "text": "Ship", "tone": "accent" }] }""").ShouldBe(0);
    }

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(CloseLayout.Definition, document.RootElement, FitWidths.Default, "dark");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(CloseLayout.Definition, document.RootElement);
    }
}
