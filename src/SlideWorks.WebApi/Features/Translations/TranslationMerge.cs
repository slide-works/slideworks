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

    // Every live slide of the deck merged in the language, in the deck's order.
    public async Task<MergedDeck> ForDeckAsync(string deckId, string? language)
        => Merge(await LoadAsync(deckId), language);

    // Loads the deck, its settings, its template and its live slides once, so several languages merge from one snapshot.
    public async Task<DeckSnapshot> LoadAsync(string deckId)
    {
        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);
        var settings = await translations.ForDeckAsync(deckId);
        var template = await templates.ForDeckAsync(deck, CancellationToken.None);
        var slides = await db.Slides.AsNoTracking().Where(s => s.DeckId == deckId && s.DeletedAt == null).OrderBy(s => s.Position).ToListAsync();
        return new DeckSnapshot(settings, template, slides);
    }

    public static MergedDeck Merge(DeckSnapshot snapshot, string? language)
        => new([.. snapshot.Slides.Select(slide => (slide.Id, For(slide, snapshot.Template, snapshot.Settings, language)))]);
}
