using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Translations;

// The one place a language is merged into slots (SW-883 K3, D15). For does it for one slide; ForDeckAsync for every live slide
// of a deck, for readers that merge several. It takes no ICurrentActor, so the audience's code can call it. Per request,
// because it uses the database.
[RegisterPerRequest]
public class TranslationMerge(AppDbContext db, IDeckTranslations translations, ITemplates templates)
{
    // The slide's slots in the language, or its own slots with no fallback lines when the language is not effective: translations
    // off, or the language is not in the deck's list (the primary never is). A layout the template does not know has no lines.
    public static MergedSlide For(Slide slide, ResolvedTemplate template, TranslationSettings settings, string? language)
    {
        var slots = SlideJson.Slots(slide);
        var layout = template.FindLayout(slide.Layout);
        if (!settings.Enabled || language is null || !settings.Languages.Contains(language, StringComparer.Ordinal) || layout is null)
        {
            return new MergedSlide(slots, 0, []);
        }

        SlideTranslationsJson.Read(slide.TranslationsJson).TryGetValue(language, out var entries);
        return SlideTranslation.Merge(layout, slots, entries);
    }

    // Every live slide of the deck merged in the language, found by slide.
    public async Task<MergedDeck> ForDeckAsync(string deckId, string? language)
    {
        var deck = await db.Decks.AsNoTracking().SingleOrDefaultAsync(d => d.Id == deckId);
        if (deck is null)
        {
            return new MergedDeck(new Dictionary<Guid, MergedSlide>());
        }

        var settings = await translations.ForDeckAsync(deckId);
        var template = await templates.ForDeckAsync(deck, CancellationToken.None);
        var slides = await db.Slides.AsNoTracking().Where(s => s.DeckId == deckId && s.DeletedAt == null).OrderBy(s => s.Position).ToListAsync();
        return new MergedDeck(slides.ToDictionary(slide => slide.Id, slide => For(slide, template, settings, language)));
    }
}
