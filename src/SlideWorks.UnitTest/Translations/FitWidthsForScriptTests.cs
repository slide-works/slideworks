using SlideWorks.Core.Slides;
using SlideWorks.Core.Translations;

namespace SlideWorks.UnitTest.Translations;

// FitWidths.ForScript (K3): the fitEm comes from the catalogue, never from a literal here.
[TestFixture]
public class FitWidthsForScriptTests
{
    [Test]
    public void ForScript_WhenLatin_ShouldReturnTemplate()
    {
        var template = new FitWidths(0.5, 0.6, 0.7);

        FitWidths.ForScript(template, "fr").ShouldBeSameAs(template);
        FitWidths.ForScript(template, "vi").ShouldBeSameAs(template);
    }

    [Test]
    public void ForScript_WhenJapanese_ShouldUseCatalogueFitEm()
    {
        var em = LanguageCatalogue.ScriptOf("ja")!.FitEm!.Value;

        FitWidths.ForScript(new FitWidths(0.5, 0.6, 0.7), "ja").ShouldBe(new FitWidths(em, em, em));
    }

    [Test]
    public void ForScript_WhenThai_ShouldUseCatalogueFitEm()
    {
        var em = LanguageCatalogue.ScriptOf("th")!.FitEm!.Value;

        FitWidths.ForScript(FitWidths.Default, "th").ShouldBe(new FitWidths(em, em, em));
    }

    [Test]
    public void ForScript_WhenCodeNotInCatalogue_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => FitWidths.ForScript(FitWidths.Default, "ar"));
    }
}
