using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class ImageLayoutTests
{
    private const string IMAGE = """{ "image": "img_sample0001", "alt": "A picture" }""";

    [Test]
    public void Validate_WhenPositionIsRight_ShouldAllowThreeHeadlineLines()
    {
        // 760 / (64 * 0.56) = 21 characters a line: three words of 20 fill three lines, and four words of 15 (two do not share a line) fill four.
        var headline = string.Join(' ', Enumerable.Repeat(new string('a', 20), 3));

        Validate($$"""{ "headline": "{{headline}}", "image": {{IMAGE}}, "position": "right" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenPositionIsRightAndTheHeadlineWrapsToFour_ShouldRefuse()
    {
        var headline = string.Join(' ', Enumerable.Repeat(new string('a', 15), 4));

        Validate($$"""{ "headline": "{{headline}}", "image": {{IMAGE}}, "position": "right" }""").Errors.ShouldContain(
            e => e.StartsWith("image.headline: too long beside the image: it wraps to about 4 lines in the left column, and at most 3 fit", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenPositionIsRightAndTheHeadlineWrapsToFour_ShouldAcceptBelow()
    {
        var headline = string.Join(' ', Enumerable.Repeat(new string('a', 15), 4));

        Validate($$"""{ "headline": "{{headline}}", "image": {{IMAGE}} }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenFrameIsNotAWord_ShouldRefuse()
    {
        Validate($$"""{ "headline": "h", "image": {{IMAGE}}, "frame": "round" }""").Errors.ShouldContain(e => e.StartsWith("image.frame", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenFrameIsNone_ShouldAccept()
    {
        Validate($$"""{ "headline": "h", "image": {{IMAGE}}, "frame": "none" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenAPointHasALead_ShouldAccept()
    {
        Validate($$"""{ "headline": "h", "image": {{IMAGE}}, "points": [{ "icon": "bolt", "lead": "Fast.", "text": "t" }, "plain"] }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenAPointIconIsUnknown_ShouldRefuse()
    {
        Validate($$"""{ "headline": "h", "image": {{IMAGE}}, "points": [{ "icon": "no-such-icon", "text": "t" }] }""").Errors.ShouldContain(e => e.StartsWith("image.points[0].icon", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        Validate($$"""{ "headline": "Small steps", "image": {{IMAGE}}, "emphasis": [{ "text": "big", "tone": "accent" }] }""").Errors.ShouldContain(e => e.StartsWith("image.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Count_WhenOneByOne_ShouldBePoints()
    {
        Count($$"""{ "headline": "h", "image": {{IMAGE}}, "points": ["a", "b", "c"], "reveal": "one-by-one" }""").ShouldBe(3);
    }

    [Test]
    public void Count_WhenNoPoints_ShouldBeZero()
    {
        Count($$"""{ "headline": "h", "image": {{IMAGE}}, "reveal": "one-by-one" }""").ShouldBe(0);
    }

    [Test]
    public void Count_WhenRevealIsAllAndCalloutRevealsLast_ShouldBeOne()
    {
        Count($$"""{ "headline": "h", "image": {{IMAGE}}, "points": ["a", "b"], "callout": { "tone": "accent", "text": "c", "reveal": "last" } }""").ShouldBe(1);
    }

    [Test]
    public void Count_WhenOneByOneAndCalloutRevealsLast_ShouldBePointsPlusOne()
    {
        Count($$"""{ "headline": "h", "image": {{IMAGE}}, "points": ["a", "b"], "reveal": "one-by-one", "callout": { "tone": "accent", "text": "c", "reveal": "last" } }""").ShouldBe(3);
    }

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(ImageLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(ImageLayout.Definition, document.RootElement);
    }
}
