using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class ChartLayoutTests
{
    private const string BASE = """ "kind": "bar", "xLabels": ["a", "b"], "series": [{ "name": "s", "values": [1, 2] }] """;

    [Test]
    public void Validate_WhenChartHasACallout_ShouldAccept()
    {
        Validate($$"""{ "headline": "Lead time", {{BASE}}, "caption": "Median.", "callout": { "tone": "info", "text": "Cut the batch, not the scope." } }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenCalloutRevealsLast_ShouldBeOne()
    {
        Count($$"""{ "headline": "Lead time", {{BASE}}, "callout": { "tone": "info", "text": "c", "reveal": "last" } }""").ShouldBe(1);
        Count($$"""{ "headline": "Lead time", {{BASE}}, "callout": { "tone": "info", "text": "Cut the batch, not the scope." } }""").ShouldBe(0);
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseOccursTwice_ShouldRefuse()
    {
        Validate($$"""{ "headline": "Go fast and ship fast", {{BASE}}, "emphasis": [{ "text": "fast", "tone": "accent" }] }""").Errors.ShouldHaveSingleItem()
            .ShouldStartWith("chart.emphasis[0].text:");
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsInTheHeadline_ShouldAccept()
    {
        Validate($$"""{ "headline": "Lead time fell", {{BASE}}, "emphasis": [{ "text": "fell", "tone": "positive" }] }""").Errors.ShouldBeEmpty();
    }

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(ChartLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(ChartLayout.Definition, document.RootElement);
    }
}
