using SlideWorks.Core.Slides.Layouts;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Translations;
using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K3: TranslationMerge.For applies one language to a slide through the template's layout.
[TestFixture]
public class TranslationMergeTests
{
    private const string TH_LINES = """{"th":{"/quote":{"text":"สวัสดี","from":"Hello"}}}""";

    private static readonly TranslationSettings _on = new(true, "en", ["th"]);

    private static ResolvedTemplate Template() => new(new TemplateCatalogue().Default, "default", null, true);

    private static Slide Quote(string layout = "quote") => new()
    {
        DeckId = "deck",
        Layout = layout,
        SlotsJson = """{"quote":"Hello","attribution":"Sam Rivera"}""",
        TranslationsJson = TH_LINES,
    };

    private static string QuoteOf(MergedSlide merged) => merged.Slots.GetProperty("quote").GetString()!;

    [Test]
    public void For_WhenTranslationsOff_ShouldReturnOwnSlots()
    {
        var merged = TranslationMerge.For(Quote(), Template(), _on with { Enabled = false }, "th");

        QuoteOf(merged).ShouldBe("Hello");
        merged.FallbackLines.ShouldBe(0);
        merged.Lines.ShouldBeEmpty();
    }

    [Test]
    public void For_WhenLanguageIsPrimary_ShouldReturnOwnSlots()
    {
        var merged = TranslationMerge.For(Quote(), Template(), _on, "en");

        QuoteOf(merged).ShouldBe("Hello");
        merged.FallbackLines.ShouldBe(0);
    }

    [Test]
    public void For_WhenLanguageNotInList_ShouldReturnOwnSlots()
    {
        var merged = TranslationMerge.For(Quote(), Template(), _on, "ja");

        QuoteOf(merged).ShouldBe("Hello");
        merged.FallbackLines.ShouldBe(0);
    }

    [Test]
    public void For_WhenCurrentLine_ShouldReturnTranslation()
    {
        var merged = TranslationMerge.For(Quote(), Template(), _on, "th");

        QuoteOf(merged).ShouldBe("สวัสดี");
        merged.Lines.ShouldContain(line => line.Path == "/quote" && line.State == LineState.Current);
        merged.FallbackLines.ShouldBe(1);
    }

    [Test]
    public void For_WhenCustomLayout_ShouldResolveFromTemplate()
    {
        var template = Template() with { Layouts = name => name == "my-quote" ? QuoteLayout.Definition : null };

        var merged = TranslationMerge.For(Quote("my-quote"), template, _on, "th");

        QuoteOf(merged).ShouldBe("สวัสดี");
    }

    [Test]
    public void For_WhenLayoutUnknown_ShouldHaveNoLines()
    {
        var merged = TranslationMerge.For(Quote("no-such-layout"), Template(), _on, "th");

        QuoteOf(merged).ShouldBe("Hello");
        merged.Lines.ShouldBeEmpty();
        merged.FallbackLines.ShouldBe(0);
    }
}
