using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Decks;

// Renames a deck, for set_title: its title, and its subtitle and author when they are sent. The owner or a person with write
// (C5); anyone else, like an unknown deck, gets "Deck not found". The fields are checked by create_deck's rules. The slug
// follows the new title; the old address still works, because the player redirects any slug to the deck's current one. One
// call takes the deck's next revision and records one activity entry in one transaction, then publishes DeckChanged.
[RegisterPerRequest]
public class TitleCommands(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IDeckEvents events,
    TimeProvider clock)
{
    public const string TOOL = "set_title";

    // subtitle and author: null keeps the current value, an empty string clears it.
    public async Task<DeckTitle> SetAsync(string deckId, string? title, string? subtitle, string? author, string? tool)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var newTitle = DeckCommands.Required("title", title, DeckCommands.TITLE_MAX_LENGTH);
        var newSubtitle = DeckCommands.Optional("subtitle", subtitle, DeckCommands.SUBTITLE_MAX_LENGTH);
        var newAuthor = DeckCommands.Optional("author", author, DeckCommands.AUTHOR_MAX_LENGTH);

        DeckTitle result;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var deck = await db.Decks.SingleAsync(d => d.Id == deckId);
            deck.Title = newTitle;
            deck.Slug = DeckIds.Slugify(newTitle);
            if (subtitle is not null)
            {
                deck.Subtitle = newSubtitle;
            }
            if (author is not null)
            {
                deck.Author = newAuthor;
            }
            deck.Revision += 1;
            deck.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            await activity.RecordAsync(deckId, ActivityEntry.By(actor, tool, Summary(newTitle)));
            await transaction.CommitAsync();
            result = new DeckTitle(deck.Title, deck.Subtitle, deck.Author, deck.Slug);
        }

        await events.PublishAsync(new DeckChanged(deckId));
        return result;
    }

    public static string Summary(string title) => $"renamed the deck to “{title}”";
}
