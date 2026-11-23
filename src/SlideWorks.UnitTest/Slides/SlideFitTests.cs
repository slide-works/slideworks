using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides;

// SlideFit is internal, so its wrapping is seen through the validator: the same slide at two widths.
[TestFixture]
public class SlideFitTests
{
    // Eight points of 69 characters: one line each at 0.56 em (71 characters a line in the bullets
    // list), two lines each at 0.62 em (64 a line), which no longer fits the slide.
    private static readonly string _point = string.Join(" ", Enumerable.Repeat("abcdefghi", 7));

    [Test]
    public void Lines_WhenWidthsAreWider_ShouldWrapEarlier()
    {
        var slots = Slots(new { headline = "Eight points", items = Enumerable.Repeat(_point, 8) });

        var atDefault = SlideValidator.Validate(BulletsLayout.Definition, slots, FitWidths.Default);
        var wider = SlideValidator.Validate(BulletsLayout.Definition, slots, new FitWidths(0.62, 0.62, 0.62));

        atDefault.Errors.ShouldBeEmpty();
        wider.Errors.ShouldHaveSingleItem().ShouldStartWith("bullets.items: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenSurfaceIsGiven_ShouldStillValidateTheSlots()
    {
        var good = Slots(new { title = "Ship smaller" });
        var bad = Slots(new { subtitle = "No title" });

        SlideValidator.Validate(TitleLayout.Definition, good, FitWidths.Default, "dark").Errors.ShouldBeEmpty();
        SlideValidator.Validate(TitleLayout.Definition, bad, FitWidths.Default, "dark").Errors.ShouldNotBeEmpty();
        SlideValidator.Validate(TitleLayout.Definition, bad, FitWidths.Default, "dark").Errors
            .ShouldBe(SlideValidator.Validate("title", bad).Errors);
    }

    // One bullet at 36 px in a 1448 px list holds 71 average characters a line: eight 8-letter words fill it exactly.
    [TestCase(8, 1)]
    [TestCase(9, 2)]
    [TestCase(16, 2)]
    [TestCase(17, 3)]
    public void Lines_WhenLowercaseProseFillsTheLine_ShouldWrapWhereItAlwaysDid(int words, int lines)
    {
        Lines(string.Join(" ", Enumerable.Repeat("abcdefgh", words))).ShouldBe(lines);
    }

    [Test]
    public void Lines_WhenTheSameWordsAreWideLetters_ShouldTakeMoreLinesThanNarrowOnes()
    {
        var narrow = string.Join(" ", Enumerable.Repeat(new string('n', 9), 6));
        var wide = string.Join(" ", Enumerable.Repeat(new string('W', 9), 6));

        Lines(wide).ShouldBeGreaterThan(Lines(narrow));
    }

    [Test]
    public void Lines_WhenTextIsAllCapitals_ShouldTakeMoreLinesThanLowercase()
    {
        var lower = string.Join(" ", Enumerable.Repeat("shipping", 8));

        Lines(lower.ToUpperInvariant()).ShouldBeGreaterThan(Lines(lower));
    }

    [Test]
    public void Lines_WhenOneWordIsLongerThanALineOfWideLetters_ShouldBreakItByWidth()
    {
        // 60 W cost 60 × 1.85 = 111 average characters: two lines of 71, where 60 n fit one.
        Lines(new string('n', 60)).ShouldBe(1);
        Lines(new string('W', 60)).ShouldBe(2);
        Lines(new string('W', 80)).ShouldBe(3);
    }

    private static int Lines(string text)
    {
        var slots = Slots(new { headline = "One point", items = new[] { text } });
        var region = SlideValidator.Validate(BulletsLayout.Definition, slots, FitWidths.Default).Regions.Single();

        return (int)Math.Round(region.Height / 48.6);
    }

    private static JsonElement Slots(object slots) => JsonSerializer.SerializeToElement(slots);
}
