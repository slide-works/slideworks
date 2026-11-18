using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class DividerLayoutTests
{
    [Test]
    public void Validate_WhenSubIs110_ShouldAccept()
    {
        Validate("divider", new string('a', 110)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenSubIs111_ShouldRefuse()
    {
        Validate("divider", new string('a', 111)).Errors.ShouldHaveSingleItem().ShouldStartWith("divider.sub: at most 110 characters");
    }

    private static SlideValidationResult Validate(string layout, string sub)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { title = "Slice it thin", sub }));
        return SlideValidator.Validate(layout, document.RootElement);
    }
}
