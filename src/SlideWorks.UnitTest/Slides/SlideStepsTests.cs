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

    [TestCase("numbered-cards", "cards")]
    [TestCase("cards", "cards")]
    [TestCase("rules", "rules")]
    public void Count_WhenCardsOneByOne_ShouldEqualCardCount(string layout, string list)
    {
        Count(layout, $$"""{ "{{list}}": [{}, {}], "reveal": "one-by-one" }""").ShouldBe(2);
    }

    [Test]
    public void Count_WhenLayoutHasNoRevealList_ShouldBeZero()
    {
        Count("compare", """{ "reveal": "one-by-one", "items": ["a"] }""").ShouldBe(0);
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
