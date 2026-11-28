using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;
using SlideWorks.Core.Translations;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Translations;

// TranslatableLines.Of (K3): text-slot strings only, as RFC 6901 pointers over the stored form.
[TestFixture]
public class TranslatableLinesTests
{
    private static IEnumerable<TestCaseData> SampleSlides() =>
        SampleDeck.Slides.Select(slide => new TestCaseData(slide.GetProperty("number").GetInt32())
            .SetArgDisplayNames($"slide {slide.GetProperty("number").GetInt32()} ({slide.GetProperty("layout").GetString()})"));

    [Test]
    public void Of_WhenBulletsHasStringAndObjectItems_ShouldPointAtStoredForm()
    {
        var lines = Lines(BulletsLayout.Definition, """
            { "headline": "Small batches win", "style": "numbered",
              "items": ["A plain point", { "lead": "Risk shows up late.", "text": "Customers see it first." }, { "icon": "rocket", "text": "Ship it" }],
              "reveal": "one-by-one" }
            """);

        lines.ShouldBe([
            new SlotLine("/headline", "Small batches win"),
            new SlotLine("/items/0", "A plain point"),
            new SlotLine("/items/1/lead", "Risk shows up late."),
            new SlotLine("/items/1/text", "Customers see it first."),
            new SlotLine("/items/2/text", "Ship it"),
        ]);
    }

    [Test]
    public void Of_WhenTableHasToneCells_ShouldPointAtText()
    {
        var lines = Lines(TableLayout.Definition, """
            { "headline": "Sizing", "columns": ["", "Verdict"],
              "rows": [["One quarter", { "text": "Avoid", "tone": "bad" }]], "highlightRow": 1 }
            """);

        lines.ShouldBe([
            new SlotLine("/headline", "Sizing"),
            new SlotLine("/columns/1", "Verdict"),
            new SlotLine("/rows/0/0", "One quarter"),
            new SlotLine("/rows/0/1/text", "Avoid"),
        ]);
    }

    [Test]
    public void Of_WhenSlotIsEnumOrImage_ShouldSkipIt()
    {
        var lines = Lines(QuoteLayout.Definition, """
            { "quote": "Ship smaller", "attribution": "Sam", "image": { "image": "img_1", "alt": "A portrait of Sam" }, "reveal": "all" }
            """);

        lines.ShouldBe([new SlotLine("/quote", "Ship smaller"), new SlotLine("/attribution", "Sam")]);
    }

    [Test]
    public void Of_WhenCalloutAndEmphasis_ShouldListTheirText()
    {
        var lines = Lines(BulletsLayout.Definition, """
            { "headline": "Small batches win", "emphasis": [{ "text": "Small batches", "tone": "accent", "in": "headline" }],
              "items": ["A point"], "callout": { "tone": "positive", "fill": "solid", "text": "Start on Monday", "reveal": "last" } }
            """);

        lines.ShouldBe([
            new SlotLine("/headline", "Small batches win"),
            new SlotLine("/emphasis/0/text", "Small batches"),
            new SlotLine("/items/0", "A point"),
            new SlotLine("/callout/text", "Start on Monday"),
        ]);
    }

    [TestCaseSource(nameof(SampleSlides))]
    public void Of_WhenSampleDeckSlide_ShouldResolveEveryPath(int number)
    {
        var slide = SampleDeck.Slide(number);
        var slots = slide.GetProperty("slots");

        var lines = TranslatableLines.Of(LayoutCatalogue.Find(slide.GetProperty("layout").GetString()!)!, slots);

        lines.ShouldNotBeEmpty();
        lines.Select(line => line.Path).ShouldBeUnique();
        lines.Select(line => TextAt(slots, line.Path)).ShouldBe(lines.Select(line => line.Text));
    }

    private static IReadOnlyList<SlotLine> Lines(LayoutDefinition layout, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return TranslatableLines.Of(layout, document.RootElement);
    }

    // The string an RFC 6901 pointer names, or null when it names nothing or no string.
    private static string? TextAt(JsonElement root, string pointer)
    {
        var current = root;
        foreach (var token in pointer.Split('/').Skip(1).Select(t => t.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(token, out var index) && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(token, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
