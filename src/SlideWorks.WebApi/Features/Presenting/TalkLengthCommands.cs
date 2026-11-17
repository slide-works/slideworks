using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Presenting;

// Sets or clears a deck's talk length (P7): the one write path for PUT /api/decks/{id}/talk-length and set_talk_length.
// Owner only: anyone else, like an unknown deck, gets "Deck not found". The checks come before anything changes, so a
// refused call writes nothing, publishes nothing and tells no presentation. One call takes the deck's next revision and
// records one activity entry in one transaction, then publishes DeckChanged, then tells the live presentation (if any).
[RegisterPerRequest]
public class TalkLengthCommands(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IDeckEvents events,
    IPresentations presentations,
    TimeProvider clock)
{
    public const string TOOL = "set_talk_length";

    // tool is the MCP tool's name when an agent calls, for the change log, and null from the web.
    public async Task SetAsync(string deckId, int? minutes, string? tool)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        TalkLengthRequest.Check(minutes);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var deck = await db.Decks.SingleAsync(d => d.Id == deckId);
            deck.TalkMinutes = minutes;
            deck.Revision += 1;
            deck.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            await activity.RecordAsync(deckId, ActivityEntry.By(actor, tool, Summary(minutes)));
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deckId));
        await presentations.TalkLengthChangedAsync(deckId, minutes);
    }

    public static string Summary(int? minutes) => minutes is { } m ? $"set the talk length to {m} min" : "cleared the talk length";
}
