using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Decks;

// Replaces a deck's whole parts list (SW-555), for set_parts. Owner only: anyone else, like an unknown deck, gets "Deck not
// found". The list is checked by the same code as create_deck's parts. Slides keep their part numbers, so a list shorter than
// the highest part a slide uses is refused, naming those slides, and nothing is written. One call takes the deck's next
// revision and records one activity entry in one transaction, then publishes DeckChanged.
[RegisterPerRequest]
public class PartsCommands(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IDeckEvents events,
    TimeProvider clock)
{
    public const string TOOL = "set_parts";

    public async Task<IReadOnlyList<DeckPart>> SetAsync(string deckId, IReadOnlyList<JsonElement>? items, string? tool)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (items is null)
        {
            throw new BadRequestException("parts: required");
        }
        var parts = DeckCommands.Parts(items);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var deck = await db.Decks.SingleAsync(d => d.Id == deckId);
            var stranded = await db.Slides.Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Part > parts.Count).OrderBy(s => s.Position).ToListAsync();
            if (stranded.Count > 0)
            {
                var named = string.Join(", ", stranded.Select(s => $"slide {s.Position + 1} (part {s.Part})"));
                var needed = stranded.Max(s => s.Part);
                var advice = parts.Count == 0
                    ? "update_slide cannot clear a slide's part, so keep enough parts or delete those slides first"
                    : "keep at least that many parts, move those slides to a lower part with update_slide, or delete them first";
                throw new BadRequestException($"parts: slides use parts the new list does not have, so it needs at least {needed} parts: {named}. To go ahead, {advice}");
            }
            deck.PartsJson = DeckParts.Write(parts);
            deck.Revision += 1;
            deck.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            await activity.RecordAsync(deckId, ActivityEntry.By(actor, tool, Summary(parts.Count)));
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deckId));
        return parts;
    }

    public static string Summary(int count) => count == 0 ? "cleared the parts" : $"set the parts ({count})";
}
