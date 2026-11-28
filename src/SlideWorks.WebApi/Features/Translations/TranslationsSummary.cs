using Agoda.IoC.Core;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Translations.Mcp;

namespace SlideWorks.WebApi.Features.Translations;

// The K4 translations object of a deck: whether translations are on, the primary language and each language's coverage. Every
// count comes from TranslationCoverage over the merged slides, so emphasis lines are counted here (D24). It takes no
// ICurrentActor.
[RegisterPerRequest]
public class TranslationsSummary(IDeckTranslations translations, TranslationMerge merge)
{
    // Null while translations are off.
    public async Task<TranslationsSettingsView?> ForDeckAsync(string deckId)
    {
        var settings = await translations.ForDeckAsync(deckId);
        if (!settings.Enabled)
        {
            return null;
        }

        var languages = new List<LanguageCoverageView>(settings.Languages.Count);
        foreach (var code in settings.Languages)
        {
            var coverage = TranslationCoverage.Of((await merge.ForDeckAsync(deckId, code)).Slides);
            languages.Add(new LanguageCoverageView(code, LanguageCatalogue.Find(code)?.Name ?? code, coverage.Lines, coverage.Current, coverage.Stale, coverage.Missing));
        }

        return new TranslationsSettingsView(true, settings.Primary, languages);
    }
}
