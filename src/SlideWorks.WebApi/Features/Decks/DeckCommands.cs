using System.Security.Cryptography;
using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Join;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Decks;

// Creates decks (K5), for POST /api/decks and the create_deck tool. A new deck is private to the person creating it, or
// for an agent, to the presenter it acts for. Its look comes from LookRules (L3): the named or default template and
// palette, and Draft from the template's chrome. The template comes from ITemplates (T4): only one the caller may pick,
// pinned at its latest published version (null on a built-in).
[RegisterPerRequest]
public class DeckCommands(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IDeckEvents events,
    TimeProvider clock,
    ITemplates templates,
    IPaletteCatalogue palettes,
    ImageUploads images,
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
        var source = await SourceAsync(request);
        Deck deck = null!;
        string? copiedLogo = null;
        string? copiedDeckId = null;

        // The may-pick check, the look and the write are one unit: the transaction opens before the template is picked. A
        // refusal throws before anything is written, and leaving the block rolls back. A copied logo file is removed when
        // the write does not commit.
        try
        {
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var target = await PickAsync(source?.Template ?? request.Template);
            var catalogues = ResolvedCatalogues.For(palettes, target);
            var look = new LookRules(catalogues.Templates, catalogues.Palettes).Resolve(target.Ref, source?.Palette ?? request.Palette);
            if (look.Error is not null)
            {
                throw new BadRequestException(look.Error);
            }
            var template = look.Template!;
            var palette = look.Palette!;
            if (request.Colors is { Count: > 0 })
            {
                var refused = PaletteTokens.Check(palette, request.Colors);
                if (refused is not null)
                {
                    throw new BadRequestException(refused);
                }
            }

            var now = clock.GetUtcNow();
            deck = new Deck
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
                Template = target.Ref,
                TemplateVersion = source?.TemplateVersion ?? target.Version,
                Palette = palette.Name,
                PaletteTokensJson = source is null ? PaletteTokens.Write(request.Colors) : source.PaletteTokensJson,
                Draft = source?.Draft ?? template.Chrome.Draft,
                ChromeJson = source?.ChromeJson,
                TalkMinutes = source?.TalkMinutes,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await SaveWithJoinCodeAsync(deck);
            if (source?.LogoImageId is { } logoId)
            {
                var logo = await db.DeckImages.AsNoTracking().SingleOrDefaultAsync(i => i.Id == logoId && i.DeckId == source.Id && i.UploadedAt != null)
                    ?? throw new BadRequestException("like: that deck's logo is not an uploaded image");
                copiedLogo = images.CopyCompleted(logo, deck.Id);
                copiedDeckId = deck.Id;
                deck.LogoImageId = copiedLogo;
                await db.SaveChangesAsync();
            }

            var like = source is null ? "" : $"like \"{source.Title}\" ";
            await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, tool, $"created the deck {like}from the {template.Name} template with the {palette.Label} palette"));
            await transaction.CommitAsync();
            copiedLogo = null;
        }

        await events.PublishAsync(new DeckChanged(deck.Id));
        return deck;
        }
        catch
        {
            if (copiedLogo is not null && copiedDeckId is not null)
            {
                images.DeleteFile(copiedDeckId, copiedLogo);
            }

            throw;
        }
    }

    // The deck whose look a new deck copies, or null. Only the owner may copy it, as only the owner may get_deck it: a deck
    // shared by link or with named people is "Deck not found" to anyone else (SW-568). template, palette and colors stay
    // out: they would disagree with the copy.
    private async Task<Deck?> SourceAsync(CreateDeckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Like))
        {
            return null;
        }

        if (request.Template is not null || request.Palette is not null || request.Colors is { Count: > 0 })
        {
            throw new BadRequestException("like: leave template, palette and colors out; they come from that deck");
        }

        if (!await access.CanEditAsync(request.Like, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }

        return await db.Decks.AsNoTracking().SingleOrDefaultAsync(d => d.Id == request.Like) ?? throw new NotFoundException(DeckReader.NOT_FOUND);
    }

    // The template a new deck goes on: the named one, or the default built-in. Only a template the caller may pick is
    // found, so one that exists but is not theirs is refused in the same words as one that does not exist.
    private async Task<ResolvedTemplate> PickAsync(string? name)
    {
        var pickable = await templates.ListForAsync(actor, CancellationToken.None);
        if (name is null)
        {
            return pickable.Where(t => t.Template.IsDefault).OrderByDescending(t => t.BuiltIn).First();
        }
        return pickable.FirstOrDefault(t => string.Equals(t.Ref, name, StringComparison.Ordinal))
            ?? throw new BadRequestException(LookRules.UnknownTemplate(name));
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

    private static string Required(string field, string? value, int maxLength, bool showLength = false)
        => Optional(field, value, maxLength, showLength) ?? throw new BadRequestException($"{field}: required");

    private static string? Optional(string field, string? value, int maxLength, bool showLength = false)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        if (text.Length <= maxLength)
        {
            return text;
        }
        var got = showLength ? $" (got {text.Length})" : "";
        throw new BadRequestException($"{field}: at most {maxLength} characters{got}");
    }

    private static IReadOnlyList<DeckPart> Parts(IReadOnlyList<JsonElement>? items)
    {
        if (items == null)
        {
            return [];
        }
        if (items.Count > DeckParts.MAX_PARTS)
        {
            throw new BadRequestException($"parts: at most {DeckParts.MAX_PARTS} items");
        }
        var parts = items.Select((item, index) => Part($"parts[{index}]", item)).ToList();
        var firstMarked = parts.FindIndex(part => part.Mark is not null);
        if (firstMarked >= 0 && parts.Skip(firstMarked).Any(part => part.Mark is null))
        {
            throw new BadRequestException($"parts[{firstMarked}]: a part with a mark must come after every numbered part");
        }
        return parts;
    }

    private static DeckPart Part(string path, JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            return new DeckPart(Required(path, item.GetString(), DeckParts.MAX_NAME_LENGTH, showLength: true));
        }
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestException($"{path}: must be a name or {{name, short, caption, mark}}");
        }
        var unknown = item.EnumerateObject().Select(property => property.Name).FirstOrDefault(name => name is not ("name" or "short" or "caption" or "mark"));
        if (unknown is not null)
        {
            throw new BadRequestException($"{path}.{unknown}: unknown field");
        }
        return new DeckPart(
            Optional($"{path}.name", Field(path, item, "name"), DeckParts.MAX_NAME_LENGTH, showLength: true) ?? throw new BadRequestException($"{path}: required"),
            Optional($"{path}.short", Field(path, item, "short"), DeckParts.MAX_SHORT_LENGTH, showLength: true),
            Optional($"{path}.caption", Field(path, item, "caption"), DeckParts.MAX_CAPTION_LENGTH, showLength: true),
            Optional($"{path}.mark", Field(path, item, "mark"), DeckParts.MAX_MARK_LENGTH, showLength: true));
    }

    private static string? Field(string path, JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new BadRequestException($"{path}.{name}: must be text");
    }
}
