using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Translations;

// One question slide in a guest's language (SW-883 K3, D17), for the audience's reads of a poll, quiz, word-cloud or open-text
// slide. The merge is TranslationMerge.For. It takes no ICurrentActor. Per request, because it uses the database.
[RegisterPerRequest]
public class GuestSlideLanguage(AppDbContext db, IDeckTranslations translations, ITemplates templates)
{
    // The slide with the language applied, or its own slots when the deck does not offer the language (null included).
    public async Task<MergedSlide> MergeAsync(Slide slide, string? language)
    {
        if (language is null)
        {
            return new MergedSlide(SlideJson.Slots(slide), 0, []);
        }

        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == slide.DeckId);
        var settings = await translations.ForDeckAsync(slide.DeckId);
        return TranslationMerge.For(slide, await templates.ForDeckAsync(deck, CancellationToken.None), settings, language);
    }

    // A closed round's option texts as the round ran with them. An option is translated only when its entry's From equals the
    // snapshot text (D17); otherwise it shows the primary (snapshot) text.
    public async Task<IReadOnlyList<string>> SnapshotOptionsAsync(Slide slide, string? language, IReadOnlyList<string> snapshot)
    {
        var settings = await translations.ForDeckAsync(slide.DeckId);
        if (!settings.Enabled || language is null || !settings.Languages.Contains(language, StringComparer.Ordinal)
            || !SlideTranslationsJson.Read(slide.TranslationsJson).TryGetValue(language, out var entries))
        {
            return snapshot;
        }

        return [.. snapshot.Select((text, index) =>
            entries.TryGetValue($"/options/{index}", out var entry) && string.Equals(entry.From, text, StringComparison.Ordinal) ? entry.Text : text)];
    }
}
