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

    private static JsonElement Slots(object slots) => JsonSerializer.SerializeToElement(slots);
}
