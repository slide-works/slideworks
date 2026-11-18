using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class RulesLayoutTests
{
    private const string CALLOUT = """, "callout": { "tone": "info", "text": "Keep it short" }""";

    [Test]
    public void Validate_WhenFourRulesAreAtTheirLimits_ShouldAccept()
    {
        Validate(FourRules(string.Empty)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenRulesHaveACallout_ShouldCountItsHeight()
    {
        Validate(FourRules(CALLOUT)).Errors.ShouldContain(e => e.StartsWith("rules.rules", StringComparison.Ordinal) && e.Contains("too much text to fit", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenThreeRulesHaveACallout_ShouldAccept()
    {
        var slots = $$"""{ "headline": "h", "rules": [{{Rule(60, 110)}}, {{Rule(60, 110)}}, {{Rule(60, 110)}}]{{CALLOUT}} }""";

        Validate(slots).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenOneByOneAndCalloutRevealsLast_ShouldBeRulesPlusOne()
    {
        var slots = """{ "headline": "h", "rules": [{ "title": "a", "text": "b" }, { "title": "c", "text": "d" }], "reveal": "one-by-one", "callout": { "tone": "info", "text": "c", "reveal": "last" } }""";

        Count(slots).ShouldBe(3);
    }

    [Test]
    public void Count_WhenOneByOne_ShouldBeRules()
    {
        var slots = """{ "headline": "h", "rules": [{ "title": "a", "text": "b" }, { "title": "c", "text": "d" }], "reveal": "one-by-one" }""";

        Count(slots).ShouldBe(2);
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        var slots = """{ "headline": "Small steps", "rules": [{ "title": "a", "text": "b" }], "emphasis": [{ "text": "big", "tone": "accent" }] }""";

        Validate(slots).Errors.ShouldContain(e => e.StartsWith("rules.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsInTheHeadline_ShouldAccept()
    {
        var slots = """{ "headline": "Small steps", "rules": [{ "title": "a", "text": "b" }], "emphasis": [{ "text": "Small", "tone": "positive" }] }""";

        Validate(slots).Errors.ShouldBeEmpty();
    }

    private static string Rule(int title, int text) => $$"""{ "title": "{{new string('a', title)}}", "text": "{{new string('b', text)}}" }""";

    private static string FourRules(string extra) =>
        $$"""{ "headline": "h", "rules": [{{Rule(10, 90)}}, {{Rule(10, 90)}}, {{Rule(10, 90)}}, {{Rule(10, 90)}}]{{extra}} }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(RulesLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(RulesLayout.Definition, document.RootElement);
    }
}
