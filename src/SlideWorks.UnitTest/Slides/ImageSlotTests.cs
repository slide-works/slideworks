using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// K17: an image's alt text is never drawn, so it may run to 500 characters.
[TestFixture]
public class ImageSlotTests
{
    [Test]
    public void Validate_WhenAltIs500_ShouldAccept()
    {
        var result = ValidateImageSlide(new string('a', 500));

        result.Errors.ShouldBeEmpty();
        result.ImageIds.ShouldBe(["img_1"]);
    }

    [Test]
    public void Validate_WhenAltIs501_ShouldRefuse()
    {
        var result = ValidateImageSlide(new string('a', 501));

        result.Errors.ShouldBe(["image.image.alt: at most 500 characters (got 501)"]);
    }

    private static SlideValidationResult ValidateImageSlide(string alt)
    {
        using var document = JsonDocument.Parse($$"""{ "headline": "h", "image": { "image": "img_1", "alt": "{{alt}}" } }""");
        return SlideValidator.Validate("image", document.RootElement);
    }
}
