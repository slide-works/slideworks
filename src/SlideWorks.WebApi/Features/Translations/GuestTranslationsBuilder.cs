using Agoda.IoC.Core;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Join;

namespace SlideWorks.WebApi.Features.Translations;

// GuestView.translations (SW-883 K5): the primary first, then the deck's languages, each with its completeness from
// TranslationCoverage. The deck is loaded once and every language merges from that snapshot. It takes no ICurrentActor.
[RegisterPerRequest]
public class GuestTranslationsBuilder(TranslationMerge merge, IDeckTranslations translations)
{
    // Null while translations are off.
    public async Task<GuestTranslations?> ForDeckAsync(string deckId)
    {
        if (!(await translations.ForDeckAsync(deckId)).Enabled)
        {
            return null;
        }

        var snapshot = await merge.LoadAsync(deckId);

        var languages = new List<GuestLanguage>(snapshot.Settings.Languages.Count + 1) { Of(deckId, snapshot.Settings.Primary, TranslationCoverage.Of(TranslationMerge.Merge(snapshot, snapshot.Settings.Primary).Slides).Complete) };
        foreach (var code in snapshot.Settings.Languages)
        {
            languages.Add(Of(deckId, code, TranslationCoverage.Of(TranslationMerge.Merge(snapshot, code).Slides).Complete));
        }

        return new GuestTranslations(snapshot.Settings.Primary, languages);
    }

    private static GuestLanguage Of(string deckId, string code, int complete)
    {
        var language = LanguageCatalogue.Find(code) ?? throw new InvalidOperationException($"Deck {deckId} offers {code}, which is not in the language catalogue.");
        return new GuestLanguage(language.Code, language.Name, language.EnglishName, language.Short, complete);
    }
}
