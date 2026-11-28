using System.Text.Json;
using SlideWorks.Core.Translations;

namespace SlideWorks.UnitTest.Translations;

// TranslationCoverage.Of (K3).
[TestFixture]
public class TranslationCoverageTests
{
    [Test]
    public void Of_WhenNoLines_ShouldBeComplete()
    {
        TranslationCoverage.Of([]).ShouldBe(new LanguageCoverage(0, 0, 0, 0, 100));
        TranslationCoverage.Of([new MergedSlide(default, 0, [])]).ShouldBe(new LanguageCoverage(0, 0, 0, 0, 100));
    }

    [Test]
    public void Of_WhenSomeStale_ShouldRoundDown()
    {
        var first = new MergedSlide(default(JsonElement), 1, [
            new LineView("/headline", "Ship smaller", "Envía menos", LineState.Current),
            new LineView("/kicker", "Offsite", "Retiro", LineState.Stale),
        ]);
        var second = new MergedSlide(default(JsonElement), 1, [
            new LineView("/title", "Thanks", "Gracias", LineState.Current),
            new LineView("/sub", "Questions?", null, LineState.Missing),
            new LineView("/items/0", "One", "Uno", LineState.Current),
            new LineView("/items/1", "Two", "Dos", LineState.Current),
        ]);

        TranslationCoverage.Of([first, second]).ShouldBe(new LanguageCoverage(6, 4, 1, 1, 66));
    }
}
