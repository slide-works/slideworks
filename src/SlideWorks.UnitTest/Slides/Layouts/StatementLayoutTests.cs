using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class StatementLayoutTests
{
    [Test]
    public void Validate_WhenStatementHasFourLines_ShouldRefuse()
    {
        Validate("""{ "statement": "One\nTwo\nThree\nFour" }""").Errors.ShouldBe(["statement.statement: at most 3 lines (got 4)"]);
    }

    [Test]
    public void Validate_WhenStatementWrapsToFourShownLines_ShouldRefuse()
    {
        // About 27 characters a line at 110 px, so lines of 28, 28 and 10 wrap to 2, 2 and 1 shown lines.
        var wrapped = new string('a', 28);
        var errors = Validate($$"""{ "statement": "{{wrapped}}\n{{wrapped}}\nbbbbbbbbbb" }""").Errors;

        errors.Count.ShouldBe(1);
        errors[0].ShouldStartWith("statement.statement: too long to fit: it wraps to about ");
        errors[0].ShouldEndWith(" lines at the statement's smallest size, and at most 3 fit");
    }

    [Test]
    public void Validate_WhenSubWrapsToThreeLines_ShouldRefuse()
    {
        // Wide letters (0.8 em) give about 37 characters a line at 56 px, so 100 characters wrap to 3.
        var sub = string.Join(' ', Enumerable.Repeat("Mmmmmmm", 13))[..100];
        var errors = Validate($$"""{ "statement": "Ship it", "sub": "{{sub}}" }""", new FitWidths(0.8, 0.8, 0.8)).Errors;

        errors.Count.ShouldBe(1);
        errors[0].ShouldStartWith("statement.sub: too long to fit: it wraps to about ");
        errors[0].ShouldEndWith(" lines, and at most 2 fit");
    }

    [Test]
    public void Validate_WhenStatementIs71_ShouldRefuse()
    {
        var text = string.Join(' ', Enumerable.Repeat("abcdefg", 9))[..71];
        Validate($$"""{ "statement": "{{text}}" }""").Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Validate_WhenStatementIs70AndSubIs100_ShouldAccept()
    {
        var text = "Small batches are how we find out sooner, so we ship half and learn the rest"[..70];
        var sub = string.Join(' ', Enumerable.Repeat("then learn", 10))[..100];
        Validate($$"""{ "kicker": "The point", "statement": "{{text}}", "sub": "{{sub}}", "reveal": "one-by-one" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenSubAndOneByOne_ShouldBeOne()
    {
        Count("""{ "statement": "Ship it", "sub": "Then learn", "reveal": "one-by-one" }""").ShouldBe(1);
    }

    [Test]
    public void Count_WhenNoSub_ShouldBeZero()
    {
        Count("""{ "statement": "Ship it", "reveal": "one-by-one" }""").ShouldBe(0);
    }

    [Test]
    public void Count_WhenSubWithoutOneByOne_ShouldBeZero()
    {
        Count("""{ "statement": "Ship it", "sub": "Then learn" }""").ShouldBe(0);
    }

    [Test]
    public void Validate_WhenEmphasisNamesTheSub_ShouldAccept()
    {
        Validate("""{ "statement": "Ship it", "sub": "Then learn", "emphasis": [{ "text": "learn", "tone": "accent", "in": "sub" }] }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenEmphasisNamesASubThatIsNotSet_ShouldRefuse()
    {
        Validate("""{ "statement": "Ship it", "emphasis": [{ "text": "Ship", "tone": "accent", "in": "sub" }] }""").Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Validate_WhenStatementHasACallout_ShouldRefuse()
    {
        Validate("""{ "statement": "Ship it", "callout": { "tone": "info", "text": "c" } }""").Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Validate_WhenStoredStubSlide_ShouldAccept()
    {
        Validate("""{ "kicker": "The point", "statement": "Small batches are how we find out sooner." }""").Errors.ShouldBeEmpty();
    }

    private static SlideValidationResult Validate(string slots, FitWidths? widths = null)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(StatementLayout.Definition, document.RootElement, widths ?? FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(StatementLayout.Definition, document.RootElement);
    }
}
