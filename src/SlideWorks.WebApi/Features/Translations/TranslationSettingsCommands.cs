using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Translations.Data;
using SlideWorks.WebApi.Features.Translations.Mcp;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Translations;

// Changes a deck's translation settings (SW-883 K4 set_translations): the one writer of DeckTranslationSettingsRow. The
// caller (TranslationsAgentAccess) has already checked that the actor may edit the deck.
// - Every argument is checked before anything is written, so a refusal changes nothing, records nothing and publishes
//   nothing. Only the arguments given change; the rest keeps its value.
// - A call that changes nothing writes, records and publishes nothing. Otherwise one transaction makes or changes the
//   settings row, takes the deck's next revision and records one activity entry; after the commit it publishes DeckChanged
//   and refreshes the audience.
// - Turning translations off, or taking a language out of the list, keeps the stored lines (D14); this class never touches
//   a slide's TranslationsJson.
// Per request.
[RegisterPerRequest]
public class TranslationSettingsCommands(
    AppDbContext db,
    IActivityLog activity,
    IDeckEvents events,
    IAudienceRelay relay,
    ICurrentActor actor,
    TimeProvider clock)
{
    // The MCP tool an agent changes these through, named in its activity entries.
    public const string TOOL = "set_translations";
    public const string NOTHING_GIVEN = "give at least one of enabled, primary or languages";

    public async Task ChangeAsync(string deckId, SetTranslationsChange change)
    {
        if (change is { Enabled: null, Primary: null, Languages: null })
        {
            throw new BadRequestException(NOTHING_GIVEN);
        }
        if (change.Primary != null)
        {
            CheckSupported(change.Primary);
        }
        var languages = change.Languages?.Distinct(StringComparer.Ordinal).ToList();
        languages?.ForEach(CheckSupported);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var row = await db.DeckTranslationSettings.SingleOrDefaultAsync(s => s.DeckId == deckId);
            var before = row?.ToSettings() ?? TranslationSettings.Default;
            var after = new TranslationSettings(change.Enabled ?? before.Enabled, change.Primary ?? before.Primary, languages ?? before.Languages);
            if (after.Languages.Contains(after.Primary, StringComparer.Ordinal))
            {
                throw new BadRequestException($"{after.Primary} can't be both the primary language and a translation");
            }
            if (after.Enabled == before.Enabled && after.Primary == before.Primary && after.Languages.SequenceEqual(before.Languages, StringComparer.Ordinal))
            {
                return;
            }

            if (row == null)
            {
                db.DeckTranslationSettings.Add(DeckTranslationSettingsRow.From(deckId, after));
            }
            else
            {
                db.Entry(row).CurrentValues.SetValues(DeckTranslationSettingsRow.From(deckId, after));
            }
            var deck = await db.Decks.SingleAsync(d => d.Id == deckId);
            deck.Revision += 1;
            deck.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            await activity.RecordAsync(deckId, ActivityEntry.By(actor, actor.Via == ActorVia.Mcp ? TOOL : null, Summary(before, after)));
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deckId));
        await relay.RefreshAsync(deckId);
    }

    private static void CheckSupported(string code)
    {
        if (LanguageCatalogue.IsRightToLeft(code))
        {
            throw new BadRequestException($"right-to-left languages are not supported yet: {code}");
        }
        if (LanguageCatalogue.Find(code) == null)
        {
            throw new BadRequestException($"{code} is not a supported language; use one of: {string.Join(", ", LanguageCatalogue.All.Select(l => l.Code))}");
        }
    }

    // What changed, such as "turned translations on, with English as the primary language, with Thai and Japanese". The
    // primary and the list are named when they changed or when translations are turned on.
    private static string Summary(TranslationSettings before, TranslationSettings after)
    {
        var parts = new List<string>();
        var turnedOn = after.Enabled && !before.Enabled;
        if (after.Enabled != before.Enabled)
        {
            parts.Add($"turned translations {(after.Enabled ? "on" : "off")}");
        }
        if (turnedOn || after.Primary != before.Primary)
        {
            parts.Add($"with {NameOf(after.Primary)} as the primary language");
        }
        if (turnedOn || !after.Languages.SequenceEqual(before.Languages, StringComparer.Ordinal))
        {
            parts.Add(after.Languages.Count == 0 ? "with no other languages yet" : $"with {Join(after.Languages.Select(NameOf).ToList())}");
        }
        return string.Join(", ", parts);
    }

    private static string NameOf(string code) => LanguageCatalogue.Find(code)?.EnglishName ?? code;

    // "Thai", "Thai and Japanese", "Thai, Japanese and Korean".
    private static string Join(IReadOnlyList<string> names)
        => names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
}
