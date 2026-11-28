using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Translations;

// Reads the deck's settings row, or TranslationSettings.Default when it has none. Per request, because it uses the database.
[RegisterPerRequest(For = typeof(IDeckTranslations))]
public class DeckTranslations(AppDbContext db) : IDeckTranslations
{
    public async Task<TranslationSettings> ForDeckAsync(string deckId)
        => (await db.DeckTranslationSettings.AsNoTracking().SingleOrDefaultAsync(s => s.DeckId == deckId))?.ToSettings() ?? TranslationSettings.Default;
}
