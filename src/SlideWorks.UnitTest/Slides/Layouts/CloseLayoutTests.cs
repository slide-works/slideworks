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
