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

    [Test]
    public void Validate_WhenTheCaptionIsTallerThanTheAside_ShouldRefuse()
    {
        // Wide letters fill the 408 px column in fewer characters, so 160 of them stand taller than the body once
        // a three-series legend shares the aside.
        const string THREE = """ "kind": "bar", "xLabels": ["a", "b"], "series": [{ "name": "One", "values": [1, 2] }, { "name": "Two", "values": [1, 2] }, { "name": "Three", "values": [1, 2] }] """;
        Validate($$"""{ "headline": "Lead time", {{THREE}}, "caption": "{{new string('W', 160)}}" }""").Errors.ShouldHaveSingleItem()
            .ShouldBe("chart.caption: too much text to fit beside the chart; shorten the caption");
        Validate($$"""{ "headline": "Lead time", {{THREE}}, "caption": "{{string.Join(' ', Enumerable.Repeat("words", 26))}}" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenALegendAndACalloutShareTheBodyWithTheCaption_ShouldRefuseALongCaption()
    {
        var caption = string.Join(' ', Enumerable.Repeat("words", 26));
        var series = """[{ "name": "One", "values": [1, 2] }, { "name": "Two", "values": [1, 2] }, { "name": "Three", "values": [1, 2] }]""";
        string Slots(string callout) => $$"""{ "headline": "Lead time", "kind": "bar", "xLabels": ["a", "b"], "series": {{series}}, "caption": "{{caption}}", "callout": { "tone": "info", "text": "{{callout}}" } }""";
        Validate(Slots("Short.")).Errors.ShouldBeEmpty();
        Validate(Slots(new string('W', 220))).Errors.ShouldHaveSingleItem()
            .ShouldBe("chart.caption: too much text to fit beside the chart; shorten the caption");
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
