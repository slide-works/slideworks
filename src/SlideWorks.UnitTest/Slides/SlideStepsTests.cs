using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class SlideStepsTests
{
    [Test]
    public void Count_WhenBulletsOneByOne_ShouldEqualItemCount()
    {
        Count("bullets", """{ "headline": "h", "items": ["a", "b", "c"], "reveal": "one-by-one" }""").ShouldBe(3);
    }

    [TestCase("bullets", """{ "headline": "h", "items": ["a", "b", "c"], "reveal": "all" }""")]
    [TestCase("bullets", """{ "headline": "h", "items": ["a", "b", "c"] }""")]
    [TestCase("rules", """{ "headline": "h", "rules": [{ "title": "a", "text": "b" }], "reveal": "all" }""")]
    public void Count_WhenRevealAll_ShouldBeZero(string layout, string slots)
    {
        Count(layout, slots).ShouldBe(0);
    }

    [TestCase("numbered-cards", "cards", """{ "title": "t", "text": "x" }""")]
    [TestCase("cards", "cards", """{ "title": "t", "text": "x", "chips": [] }""")]
    [TestCase("rules", "rules", """{ "title": "t", "text": "x" }""")]
    public void Count_WhenCardsOneByOne_ShouldEqualCardCount(string layout, string list, string card)
    {
        var slots = $$"""{ "headline": "h", "{{list}}": [{{card}}, {{card}}], "reveal": "one-by-one" }""";
        SlideValidator.Validate(layout, JsonDocument.Parse(slots).RootElement).Errors.ShouldBeEmpty();

        Count(layout, slots).ShouldBe(2);
    }

    [Test]
    public void Count_WhenLayoutHasNoRevealList_ShouldBeZero()
    {
        Count("compare", """
            { "headline": "h", "failure": { "label": "l", "title": "t", "points": ["p"] },
              "success": { "label": "l", "title": "t", "points": ["p"] } }
            """).ShouldBe(0);
    }

    [Test]
    public void Count_WhenSampleDeck_ShouldMatchEachSlidesSteps()
    {
        foreach (var slide in SampleDeck.Slides)
        {
            SlideSteps.Count(slide.GetProperty("layout").GetString()!, slide.GetProperty("slots"))
                .ShouldBe(slide.GetProperty("steps").GetInt32(), $"slide {slide.GetProperty("number")}");
        }
    }

    private static int Count(string layout, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(layout, document.RootElement);
    }
}
