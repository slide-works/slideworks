using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class GanttLayoutTests
{
    private const string BASE = """ "columns": ["a", "b"], "rows": [{ "label": "r", "start": 1, "end": 2, "status": "done" }] """;

    [Test]
    public void Validate_WhenGanttHasACallout_ShouldAccept()
    {
        Validate($$"""{ "headline": "Plan", {{BASE}}, "caption": "Why.", "callout": { "tone": "info", "text": "Cut the batch, not the scope." } }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenCalloutRevealsLast_ShouldBeOne()
    {
        Count($$"""{ "headline": "Plan", {{BASE}}, "callout": { "tone": "info", "text": "c", "reveal": "last" } }""").ShouldBe(1);
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        Validate($$"""{ "headline": "Plan", {{BASE}}, "emphasis": [{ "text": "slip", "tone": "negative" }] }""").Errors.ShouldNotBeEmpty();
    }

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(GanttLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(GanttLayout.Definition, document.RootElement);
    }
}
