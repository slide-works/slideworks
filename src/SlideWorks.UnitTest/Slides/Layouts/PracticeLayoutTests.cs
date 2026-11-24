using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class PracticeLayoutTests
{
    private const string BASE = """ "headline": "Slice it", "do": [{ "lead": "Start", "text": "small" }], "comes_back": ["Feedback"], "avoid": ["Layers"] """;

    [Test]
    public void Count_WhenCalloutRevealsLast_ShouldBeOne()
    {
        Count($$"""{ {{BASE}}, "callout": { "tone": "info", "text": "c", "reveal": "last" } }""").ShouldBe(1);
        Count($$"""{ {{BASE}} }""").ShouldBe(0);
    }

    [Test]
    public void Validate_WhenTheCalloutPushesTheRightColumnPastTheBody_ShouldRefuse()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("fit-fixtures/practice.json")));
        var slots = fixture.RootElement.GetProperty("practice").GetProperty("slots").GetRawText();
        Validate(slots).Errors.ShouldBeEmpty();

        // Two lines of callout text instead of one is what the callout adds past the body.
        var taller = slots.Replace("\"Every Week Teams Learn Faster When Customers See Real Progress Weekly Work\"", "\"" + new string('W', 160) + "\"");
        taller.ShouldNotBe(slots);
        Validate(taller).Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsInTheHeadline_ShouldAccept()
    {
        Validate($$"""{ {{BASE}}, "emphasis": [{ "text": "Slice", "tone": "accent" }] }""").Errors.ShouldBeEmpty();
    }

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(PracticeLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(PracticeLayout.Definition, document.RootElement);
    }
}
