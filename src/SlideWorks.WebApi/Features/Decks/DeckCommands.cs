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
// palette, and Draft from the template's chrome. Like names a deck the caller owns and copies that deck's look instead:
// template, version, palette, colours, logo bytes on a new image, chrome, draft and talk length. The template comes from
// ITemplates (T4): only one the caller may pick, pinned at its latest published version (null on a built-in), or the
// source deck's own pin when like names it.
[RegisterPerRequest]
public class DeckCommands(
    AppDbContext db,
    ICurrentActor actor,
    IActivityLog activity,
    IDeckEvents events,
    TimeProvider clock,
    ITemplates templates,
    IPaletteCatalogue palettes,
    ILogger<DeckCommands> logger,
    IDeckAccess access,
    ImageCopies images)
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
        if (request.Like is not null && (request.Template is not null || request.Palette is not null || request.Colors is not null))
        {
            throw new BadRequestException("like: leave out template, palette and colors");
        }

        Deck deck;
        // The may-pick check, the look and the write are one unit: the transaction opens before the template is picked. A
        // refusal throws before anything is written, and leaving the block rolls back. A copied logo file is deleted when
        // the transaction does not commit, so a refusal leaves no file either.
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            string? copiedLogo = null;
            try
            {
                string templateName;
                string paletteLabel;
                if (request.Like is { } like)
                {
                    (deck, templateName, paletteLabel, copiedLogo) = await CopiedAsync(like, request, title, parts);
                }
                else
                {
                    (deck, templateName, paletteLabel) = await FreshAsync(request, title, parts);
                }

                await SaveWithJoinCodeAsync(deck);
                await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, tool, $"created the deck from the {templateName} template with the {paletteLabel} palette"));
                await transaction.CommitAsync();
                copiedLogo = null;
            }
            catch
            {
                if (copiedLogo is not null)
                {
                    File.Delete(copiedLogo);
                }

                throw;
            }
        }

        await events.PublishAsync(new DeckChanged(deck.Id));
        return deck;
    }

    private async Task<(Deck Deck, string TemplateName, string PaletteLabel)> FreshAsync(CreateDeckRequest request, string title, IReadOnlyList<DeckPart> parts)
    {
        var target = await PickAsync(request.Template);
        var catalogues = ResolvedCatalogues.For(palettes, target);
        var look = new LookRules(catalogues.Templates, catalogues.Palettes).Resolve(target.Ref, request.Palette);
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
            Template = target.Ref,
            TemplateVersion = target.Version,
            Palette = palette.Name,
            PaletteTokensJson = PaletteTokens.Write(request.Colors),
            Draft = template.Chrome.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
        return (deck, template.Name, palette.Label);
    }

    // The source deck is the one get_deck would open: the owner, and anyone else hears "Deck not found". A template this
    // caller could not put on a new deck is refused the same way. The activity names the source deck's pinned template,
    // not the latest published one, and that name is known before the logo file is copied.
    private async Task<(Deck Deck, string TemplateName, string PaletteLabel, string? CopiedFile)> CopiedAsync(
        string like, CreateDeckRequest request, string title, IReadOnlyList<DeckPart> parts)
    {
        if (!await access.CanEditAsync(like, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }

        var source = await db.Decks.AsNoTracking().SingleAsync(deck => deck.Id == like);
        await PickAsync(source.Template);
        var pinned = await templates.ForDeckAsync(source, CancellationToken.None);
        var templateName = pinned.Template.Name;
        var paletteLabel = PaletteLabel(pinned, source.Palette);
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
            Template = source.Template,
            TemplateVersion = source.TemplateVersion,
            Palette = source.Palette,
            PaletteTokensJson = source.PaletteTokensJson,
            ChromeJson = source.ChromeJson,
            Draft = source.Draft,
            TalkMinutes = source.TalkMinutes,
            CreatedAt = now,
            UpdatedAt = now,
        };
        string? copied = null;
        if (source.LogoImageId is { } logo)
        {
            (deck.LogoImageId, copied) = await images.CopyOntoAsync(source.Id, logo, deck.Id, now);
        }

        return (deck, templateName, paletteLabel, copied);
    }

    private string PaletteLabel(ResolvedTemplate template, string name)
    {
        var palette = string.Equals(name, ResolvedCatalogues.OWN_PALETTE, StringComparison.Ordinal)
            ? template.OwnPalette
            : palettes.Find(name);
        return palette?.Label ?? throw new InvalidOperationException($"A deck look names an unknown palette '{name}'.");
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
