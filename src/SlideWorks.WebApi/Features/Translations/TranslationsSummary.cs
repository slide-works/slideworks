using Agoda.IoC.Core;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Translations.Mcp;

namespace SlideWorks.WebApi.Features.Translations;

// The K4 translations object of a deck: whether translations are on, the primary language and each language's coverage. Every
// count comes from TranslationCoverage over the merged slides, so emphasis lines are counted here (D24). It takes no
// ICurrentActor.
[RegisterPerRequest]
public class TranslationsSummary(TranslationMerge merge)
{
    // Null while translations are off.
    public async Task<TranslationsSettingsView?> ForDeckAsync(string deckId)
    {
        // One snapshot for the settings and every language, so all the counts agree.
        var snapshot = await merge.LoadAsync(deckId);
        if (!snapshot.Settings.Enabled)
        {
            return null;
        }

        var languages = new List<LanguageCoverageView>(snapshot.Settings.Languages.Count);
        foreach (var code in snapshot.Settings.Languages)
        {
            var coverage = TranslationCoverage.Of(TranslationMerge.Merge(snapshot, code).Slides);
            languages.Add(new LanguageCoverageView(code, (LanguageCatalogue.Find(code) ?? throw new InvalidOperationException($"Deck {deckId} offers {code}, which is not in the language catalogue.")).Name, coverage.Lines, coverage.Current, coverage.Stale, coverage.Missing));
        }

        return new TranslationsSettingsView(true, snapshot.Settings.Primary, languages);
    }
}
