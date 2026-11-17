using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Join;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Decks;

// Creates decks (K5), for POST /api/decks and the create_deck tool. A new deck is private to the person creating it, or
// for an agent, to the presenter it acts for. Its look comes from LookRules (L3): the named or default template and
// palette, and Draft from the template's chrome.
[RegisterPerRequest]
public class DeckCommands(
    AppDbContext db,
    ICurrentActor actor,
    IActivityLog activity,
    IDeckEvents events,
    TimeProvider clock,
    ITemplateCatalogue templates,
    IPaletteCatalogue palettes,
    ILogger<DeckCommands> logger)
{
    private const int TITLE_MAX_LENGTH = 120;
    private const int SUBTITLE_MAX_LENGTH = 200;
    private const int AUTHOR_MAX_LENGTH = 80;
    private const int JOIN_CODE_TRIES = 5;
    private const int SQLITE_CONSTRAINT = 19;

    // tool is the MCP tool's name when an agent calls, for the change log, and null from the web.
    public async Task<Deck> CreateAsync(CreateDeckRequest request, string? tool)
    {
        var title = Required("title", request.Title, TITLE_MAX_LENGTH);
        var parts = Parts(request.Parts);
        var look = new LookRules(templates, palettes).Resolve(request.Template, request.Palette);
        if (look.Error is not null)
        {
            throw new BadRequestException(look.Error);
        }
        var template = look.Template!;
        var palette = look.Palette!;
        var now = clock.GetUtcNow();
        var deck = new Deck
        {
            Id = DeckIds.New(),
            Slug = DeckIds.Slugify(title),
            Title = title,
            Subtitle = Optional("subtitle", request.Subtitle, SUBTITLE_MAX_LENGTH),
            Author = Optional("author", request.Author, AUTHOR_MAX_LENGTH),
            PartsJson = DeckParts.Write(parts),
            OwnerId = actor.PresenterId ?? throw new InvalidOperationException("Creating a deck needs a signed-in caller."),
            Sharing = DeckSharing.Private,
            Revision = 0,
            Template = template.Name,
            Palette = palette.Name,
            Draft = template.Chrome.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await SaveWithJoinCodeAsync(deck);
            await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, tool, $"created the deck from the {template.Name} template with the {palette.Label} palette"));
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deck.Id));
        return deck;
    }

    // Assigns the standing join code (J1). A unique-index conflict on the code draws a new one, at most JOIN_CODE_TRIES in all.
    private async Task SaveWithJoinCodeAsync(Deck deck)
    {
        using var random = RandomNumberGenerator.Create();
        for (var attempt = 1; attempt <= JOIN_CODE_TRIES; attempt++)
        {
            deck.JoinCode = JoinCodes.New(random);
            db.Decks.Add(deck);
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (IsJoinCodeConflict(ex))
            {
                db.Entry(deck).State = EntityState.Detached;
            }
        }
        logger.LogError("No free join code for deck {DeckId} after {Tries} tries", deck.Id, JOIN_CODE_TRIES);
        throw new InvalidOperationException($"No free join code for deck {deck.Id} after {JOIN_CODE_TRIES} tries.");
    }

    private static bool IsJoinCodeConflict(DbUpdateException ex)
        => ex.InnerException is SqliteException { SqliteErrorCode: SQLITE_CONSTRAINT } inner && inner.Message.Contains("Decks.JoinCode", StringComparison.Ordinal);

    private static string Required(string field, string? value, int maxLength)
        => Optional(field, value, maxLength) ?? throw new BadRequestException($"{field}: required");

    private static string? Optional(string field, string? value, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        return text.Length <= maxLength ? text : throw new BadRequestException($"{field}: at most {maxLength} characters");
    }

    private static IReadOnlyList<string> Parts(IReadOnlyList<string>? parts)
    {
        if (parts == null)
        {
            return [];
        }
        if (parts.Count > DeckParts.MAX_PARTS)
        {
            throw new BadRequestException($"parts: at most {DeckParts.MAX_PARTS} items");
        }
        return parts.Select((part, index) => Required($"parts[{index}]", part, DeckParts.MAX_NAME_LENGTH)).ToList();
    }
}
