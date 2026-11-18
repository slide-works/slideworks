using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class TwoColumnLayoutTests
{
    [Test]
    public void Count_WhenOneByOne_ShouldBeOne()
    {
        Count(Slots("\"reveal\": \"one-by-one\"")).ShouldBe(1);
    }

    [Test]
    public void Count_WhenRevealIsAll_ShouldBeZero()
    {
        Count(Slots("\"reveal\": \"all\"")).ShouldBe(0);
    }

    [Test]
    public void Count_WhenOneByOneAndCalloutRevealsLast_ShouldBeTwo()
    {
        Count(Slots("\"reveal\": \"one-by-one\", \"callout\": { \"tone\": \"info\", \"text\": \"c\", \"reveal\": \"last\" }")).ShouldBe(2);
    }

    [Test]
    public void Validate_WhenTitleIs60_ShouldAccept()
    {
        Validate(Slots(null, new string('a', 60))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTitleIs61_ShouldRefuse()
    {
        Validate(Slots(null, new string('a', 61))).Errors.ShouldContain(e => e.StartsWith("two-column.left.title", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenAColumnIconHasNoTitle_ShouldRefuse()
    {
        var slots = """{ "headline": "h", "left": { "icon": "bolt", "text": "t" }, "right": { "text": "t" } }""";

        Validate(slots).Errors.ShouldContain("two-column.left.icon: needs left.title");
    }

    [Test]
    public void Validate_WhenTheCalloutPushesAColumnPastTheBody_ShouldRefuse()
    {
        var items = string.Join(",", Enumerable.Range(0, 6).Select(i => i < 4 ? "\"Seven words in this one point here and a few more words to wrap\"" : "\"Short point\""));
        var tall = $$"""{ "headline": "h", "left": { "items": [{{items}}] }, "right": { "text": "t" } }""";
        var withCallout = $$"""{ "headline": "h", "left": { "items": [{{items}}] }, "right": { "text": "t" }, "callout": { "tone": "info", "text": "{{new string('w', 160)}}" } }""";

        Validate(tall).Errors.ShouldBeEmpty();
        Validate(withCallout).Errors.ShouldContain(e => e.StartsWith("two-column.left", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenAnItemHasOnlyText_ShouldAccept()
    {
        var slots = """{ "headline": "h", "left": { "items": [{ "text": "t" }] }, "right": { "text": "t" } }""";

        Validate(slots).Errors.ShouldBeEmpty();
    }

    private static string Slots(string? extra, string title = "t") =>
        $$"""{ "headline": "h", "left": { "title": "{{title}}", "text": "t" }, "right": { "text": "t" }{{(extra is null ? "" : ", " + extra)}} }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(TwoColumnLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(TwoColumnLayout.Definition, document.RootElement);
    }
}
