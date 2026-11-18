using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Templates;

// IUpdateMyDecks (D14): after a template's owner publishes a version, moves the decks that person owns on the template
// to the latest published one. Only the owner may run it: anyone else, like an unknown id, gets "No template" and nothing
// changes. A template with no published version moves nothing (0). Archiving only hides a template, so an archived one
// still moves its owner's decks. The moves are one transaction: every deck whose Template is this id, whose owner is the
// caller and whose version is below the latest takes the latest, its revision goes up by one and one activity entry
// names the template and both versions. A template that locks its palette puts the deck on its own palette, as a
// template switch does. Decks that are someone else's, on another template or already current are not touched. Stored
// slides are never rewritten. Each moved deck's viewers are told with DeckChanged after the commit; a failure to tell
// them does not undo the move.
[RegisterPerRequest(For = typeof(IUpdateMyDecks))]
public sealed class UpdateMyDecks(AppDbContext db, IActivityLog activity, IDeckEvents events, TimeProvider clock) : IUpdateMyDecks
{
    public async Task<int> RunAsync(ICurrentActor actor, string templateId, CancellationToken ct)
    {
        var me = actor.PresenterId ?? throw NotFound(templateId);
        var moved = new List<string>();
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var template = await db.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == templateId && t.OwnerId == me, ct)
                ?? throw NotFound(templateId);
            var latest = await db.TemplateVersions.AsNoTracking()
                .Where(v => v.TemplateId == templateId && v.Version > TemplateVersion.DRAFT)
                .OrderByDescending(v => v.Version)
                .FirstOrDefaultAsync(ct);
            if (latest is null)
            {
                return 0;
            }

            var decks = await db.Decks
                .Where(d => d.Template == templateId && d.OwnerId == me && d.TemplateVersion < latest.Version)
                .ToListAsync(ct);
            if (decks.Count == 0)
            {
                return 0;
            }

            var manifest = ManifestJson.Parse(latest.ManifestJson).Manifest;
            var locks = manifest?.LockPalette == true;
            var label = manifest?.Label ?? template.Name;
            var now = clock.GetUtcNow();
            foreach (var deck in decks)
            {
                var from = deck.TemplateVersion!.Value;
                deck.TemplateVersion = latest.Version;
                if (locks)
                {
                    deck.Palette = ResolvedCatalogues.OWN_PALETTE;
                }
                deck.Revision += 1;
                deck.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
                await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, null, VersionMoveSummary.Of(label, from, latest.Version)));
                moved.Add(deck.Id);
            }
            await transaction.CommitAsync(ct);
        }

        foreach (var deckId in moved)
        {
            try
            {
                await events.PublishAsync(new DeckChanged(deckId));
            }
#pragma warning disable CA1031 // Telling viewers is best effort: the move has committed.
            catch (Exception)
#pragma warning restore CA1031
            {
                // The move stands.
            }
        }
        return moved.Count;
    }

    private static NotFoundException NotFound(string id) => new($"No template '{id}'.");
}
