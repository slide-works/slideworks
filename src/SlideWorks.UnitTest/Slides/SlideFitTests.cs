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
        // 37 W cost 69.19 of a 71 line, a 38th would not fit: two lines hold 74 W, three 111.
        Lines(new string('n', 60)).ShouldBe(1);
        Lines(new string('W', 60)).ShouldBe(2);
        Lines(new string('W', 74)).ShouldBe(2);
        Lines(new string('W', 75)).ShouldBe(3);
        Lines(new string('W', 80)).ShouldBe(3);
    }

    [Test]
    public void Lines_WhenWidthsDoNotDivideTheLine_ShouldNotLetAGlyphStraddleTwoLines()
    {
        // 112 W cost 209.4 = 2.95 lines of 71, but only 37 whole W fit a line (111 in three): four lines.
        Lines(new string('W', 111)).ShouldBe(3);
        Lines(new string('W', 112)).ShouldBe(4);
        Lines(new string('W', 115)).ShouldBe(4);
    }

    // Each class's widest members: the most that fit one 71-character line, then one more is a second line.
    [TestCase('%', 37)]
    [TestCase('W', 37)]
    [TestCase('@', 39)]
    [TestCase('M', 42)]
    [TestCase('m', 42)]
    [TestCase('#', 42)]
    [TestCase('w', 44)]
    [TestCase('O', 44)]
    [TestCase('Q', 44)]
    [TestCase('G', 48)]
    [TestCase('&', 48)]
    [TestCase('C', 51)]
    [TestCase('K', 51)]
    [TestCase('B', 57)]
    [TestCase('T', 57)]
    [TestCase('0', 55)]
    [TestCase('4', 58)]
    [TestCase('+', 58)]
    public void Lines_WhenAClassFillsTheLineExactly_ShouldTakeOneLineAndOneMoreTakesTwo(char letter, int fits)
    {
        Lines(new string(letter, fits)).ShouldBe(1);
        Lines(new string(letter, fits + 1)).ShouldBe(2);
    }

    [Test]
    public void Lines_WhenFactorsAreEachAtLeastOne_ShouldNeverTakeFewerLinesThanNarrowText()
    {
        foreach (var letter in "%W@MmwOQG&CBT04+")
        {
            for (var n = 1; n <= 160; n++)
            {
                Lines(new string(letter, n)).ShouldBeGreaterThanOrEqualTo(Lines(new string('n', n)));
            }
        }
    }

    private static int Lines(string text)
    {
        var slots = Slots(new { headline = "One point", items = new[] { text } });
        var region = SlideValidator.Validate(BulletsLayout.Definition, slots, FitWidths.Default).Regions.Single();

        return (int)Math.Round(region.Height / 48.6);
    }

    private static JsonElement Slots(object slots) => JsonSerializer.SerializeToElement(slots);
}
