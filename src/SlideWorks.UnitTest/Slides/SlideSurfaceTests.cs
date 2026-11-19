using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class SlideSurfaceTests
{
    private const string QUOTE = """{ "quote": "Small batches.", "attribution": "Sam" }""";

    private const string TABLE = """{ "headline": "Ship smaller", "columns": ["Was", "Now"], "rows": [["Big", "Small"]] }""";

    private const string TABLE_WITHOUT_HEADLINE = """{ "columns": ["Was", "Now"], "rows": [["Big", "Small"]] }""";

    private const string TITLE = """{ "title": "Ship smaller" }""";

    [Test]
    public void Resolve_WhenLayoutIsLightOnly_ShouldRefuseDark()
    {
        var validation = new SlotValidation();

        SlideSurface.Resolve(QaLayout.Definition, "dark", validation).ShouldBe("light");

        validation.Errors.ShouldBe(["qa.surface: this layout is light only"]);
    }

    [Test]
    public void Resolve_WhenLayoutIsDarkOnly_ShouldRefuseLight()
    {
        var validation = new SlotValidation();

        SlideSurface.Resolve(TitleLayout.Definition, "light", validation).ShouldBe("dark");

        validation.Errors.ShouldBe(["title.surface: this layout is dark only"]);
    }

    [Test]
    public void Resolve_WhenEitherAndNothingAsked_ShouldBeLight()
    {
        var validation = new SlotValidation();

        SlideSurface.Resolve(StatementLayout.Definition, null, validation).ShouldBe("light");

        validation.Errors.ShouldBeEmpty();
    }

    [TestCase("light")]
    [TestCase("dark")]
    public void Resolve_WhenEitherAndOneAsked_ShouldBeThatOne(string requested)
    {
        var validation = new SlotValidation();

        SlideSurface.Resolve(StatementLayout.Definition, requested, validation).ShouldBe(requested);

        validation.Errors.ShouldBeEmpty();
    }

    [TestCase("title", "dark", "dark")]
    [TestCase("title", null, "dark")]
    [TestCase("bullets", "light", "light")]
    [TestCase("bullets", null, "light")]
    public void Resolve_WhenFixedLayoutIsAskedItsOwnSurface_ShouldAccept(string layout, string? requested, string expected)
    {
        var validation = new SlotValidation();

        SlideSurface.Resolve(LayoutCatalogue.Find(layout)!, requested, validation).ShouldBe(expected);

        validation.Errors.ShouldBeEmpty();
    }

    [TestCase("Dark")]
    [TestCase("")]
    [TestCase("either")]
    public void Resolve_WhenAnyOtherWord_ShouldSayWhichWordsToUse(string requested)
    {
        var validation = new SlotValidation();

        SlideSurface.Resolve(StatementLayout.Definition, requested, validation).ShouldBe("light");

        validation.Errors.ShouldBe(["surface: use 'light' or 'dark'"]);
    }

    [Test]
    public void Validate_WhenALightOnlyLayoutIsAskedDark_ShouldRefuse()
    {
        var result = Validate(TableLayout.Definition, TABLE, "dark");

        result.Errors.ShouldBe(["table.surface: this layout is light only"]);
    }

    [Test]
    public void Validate_WhenADarkOnlyLayoutIsAskedLight_ShouldRefuse()
    {
        var result = Validate(TitleLayout.Definition, TITLE, "light");

        result.Errors.ShouldBe(["title.surface: this layout is dark only"]);
    }

    [TestCase(null)]
    [TestCase("light")]
    public void Validate_WhenTheSurfaceIsOneTheLayoutTakes_ShouldAccept(string? surface)
    {
        Validate(QuoteLayout.Definition, QUOTE, surface).Errors.ShouldBeEmpty();
        Validate(TitleLayout.Definition, TITLE, surface is null ? null : "dark").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheSurfaceAndASlotAreWrong_ShouldNameBoth()
    {
        var result = Validate(TableLayout.Definition, TABLE_WITHOUT_HEADLINE, "dark");

        result.Errors.ShouldBe(["table.surface: this layout is light only", "table.headline: required"]);
    }

    private static SlideValidationResult Validate(LayoutDefinition definition, string slots, string? surface)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(definition, document.RootElement, FitWidths.Default, surface);
    }
}
