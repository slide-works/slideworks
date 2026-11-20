using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class BulletsLayoutTests
{
    [Test]
    public void Validate_WhenLeadIs80_ShouldAccept()
    {
        Validate(Items($$"""{ "lead": "{{new string('a', 80)}}", "text": "t" }""")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenLeadIs81_ShouldRefuse()
    {
        Validate(Items($$"""{ "lead": "{{new string('a', 81)}}", "text": "t" }""")).Errors.ShouldContain(e => e.Contains("bullets.items[0].lead", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenAnItemHasOnlyText_ShouldAccept()
    {
        Validate(Items("""{ "text": "t" }""")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheIconIsUnknown_ShouldRefuse()
    {
        Validate(Items("""{ "icon": "no-such-icon", "text": "t" }""")).Errors.ShouldContain(e => e.StartsWith("bullets.items[0].icon", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheIconIsKnown_ShouldAccept()
    {
        Validate(Items("""{ "icon": "bolt", "text": "t" }""")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        var slots = """{ "headline": "Small steps", "items": ["a"], "emphasis": [{ "text": "big", "tone": "accent" }] }""";

        Validate(slots).Errors.ShouldContain(e => e.StartsWith("bullets.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsInTheHeadline_ShouldAccept()
    {
        var slots = """{ "headline": "Small steps", "items": ["a"], "emphasis": [{ "text": "Small", "tone": "positive" }] }""";

        Validate(slots).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenCalloutRevealsLast_ShouldBeItemsPlusOne()
    {
        var slots = """{ "headline": "h", "items": ["a", "b", "c"], "reveal": "one-by-one", "callout": { "tone": "info", "text": "c", "reveal": "last" } }""";

        Count(slots).ShouldBe(4);
    }

    [Test]
    public void Count_WhenRevealIsAllAndCalloutRevealsLast_ShouldBeOne()
    {
        var slots = """{ "headline": "h", "items": ["a", "b", "c"], "reveal": "all", "callout": { "tone": "info", "text": "c", "reveal": "last" } }""";

        Count(slots).ShouldBe(1);
    }

    [Test]
    public void Definition_WhenRead_ShouldAllowEitherSurface()
    {
        BulletsLayout.Definition.Surface.ShouldBe(LayoutSurface.Either);
    }

    private static string Items(string item) => $$"""{ "headline": "h", "items": [{{item}}] }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(BulletsLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(BulletsLayout.Definition, document.RootElement);
    }
}
