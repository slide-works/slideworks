using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Translations;
using SlideWorks.WebApi.Features.Translations.Data;

namespace SlideWorks.WebApi.Tests.Support;

// Writes translation rows straight to the database, for tests only (SW-883 D21): a deck's settings row and a slide's
// TranslationsJson. It lets a server task test against translations before translate_slides is real. It is the one place a
// test writes them, and the one exception to seeding through the API; acceptance tests seed through MCP only. It
// serialises the lines itself with System.Text.Json, as {"th": {"/title": {"text": "...", "from": "..."}}}, and does not use
// Core's serialiser, so a fault there can't hide in the seed that tests it.
public sealed class TranslationRowSeed(IServiceProvider services)
{
    // Replaces the deck's settings row, or makes it.
    public async Task SettingsAsync(string deckId, bool enabled, string primary, params string[] languages)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var next = DeckTranslationSettingsRow.From(deckId, new TranslationSettings(enabled, primary, languages));
        var row = await db.DeckTranslationSettings.SingleOrDefaultAsync(s => s.DeckId == deckId);
        if (row == null)
        {
            db.DeckTranslationSettings.Add(next);
        }
        else
        {
            db.Entry(row).CurrentValues.SetValues(next);
        }
        await db.SaveChangesAsync();
    }

    // Replaces the translated lines of the deck's live slide with this 1-based number: language, then slot path, then the line's text and the primary text it came from.
    public async Task LinesAsync(string deckId, int slide, IReadOnlyDictionary<string, IReadOnlyDictionary<string, (string Text, string From)>> lines)
    {
        var shaped = lines.ToDictionary(
            language => language.Key,
            language => language.Value.ToDictionary(line => line.Key, line => new { text = line.Value.Text, from = line.Value.From }));
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Slides.SingleAsync(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == slide - 1);
        row.TranslationsJson = JsonSerializer.Serialize(shaped);
        await db.SaveChangesAsync();
    }
}
