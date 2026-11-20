using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Decks.Mcp;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Look.Mcp;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Look;

// Changes a deck's look (L8): the one write path for PUT /api/decks/{id}/look and set_look. Owner only (K8): anyone else,
// like an unknown deck, gets "Deck not found". LookRules decides what is allowed (L3, D9): a template that locks its palette
// refuses another palette, and switching to it switches the palette too. The logo must be a completed image of this deck
// (K11). Chrome overrides merge into the deck's own, and a key set to null goes back to the template's default (D17).
// One call is one transaction that takes the deck's next revision, records one activity entry (K7) and then publishes one
// DeckChanged (K9). Slides whose layout is outside the template still render; the result warns about each one (L7).
// The template comes through ITemplates (T4): a change may move the deck only to a template the caller may pick, and a
// template they may not pick is refused in the same words as one that does not exist, before anything is written. A
// move pins the template's version on the deck (null for a built-in); naming the deck's own template again moves nothing.
[RegisterPerRequest]
public class LookCommands(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IDeckImages images,
    IActivityLog activity,
    IDeckEvents events,
    TimeProvider clock,
    ITemplates templates,
    IPaletteCatalogue palettes,
    IDeckLooks looks)
{
    public const string TOOL = "set_look";
    public const string NOTHING_TO_CHANGE = "Nothing to change";

    // tool is the MCP tool's name when an agent calls, for the change log, and null from the web.
    public async Task<LookChangeResult> SetAsync(string deckId, LookChange change, string? tool)
    {
        var (deck, template, warnings) = await ChangeAsync(deckId, change, tool);
        return new LookChangeResult(looks.PageOf(deck, template), warnings);
    }

    // set_look: the same change, with the look in get_deck's terms.
    public async Task<SetLookResult> SetFromAgentAsync(string deckId, LookChange change)
    {
        var (deck, template, warnings) = await ChangeAsync(deckId, change, TOOL);
        return new SetLookResult(GetDeckLook.From(deck, template), warnings);
    }

    public static string UnknownLogo(string imageId) => $"logo.image: unknown image {imageId}";

    private async Task<(Deck Deck, ResolvedTemplate Template, IReadOnlyList<string> Warnings)> ChangeAsync(string deckId, LookChange change, string? tool)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (change is { Template: null, Palette: null, Logo: null, Draft: null, Chrome: null, TemplateVersion: null, Colors: null })
        {
            throw new BadRequestException(NOTHING_TO_CHANGE);
        }

        Deck deck;
        ResolvedTemplate template;
        IReadOnlyList<string> warnings;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            deck = await db.Decks.SingleAsync(d => d.Id == deckId);
            var current = await templates.ForDeckAsync(deck, CancellationToken.None);
            // Judged against the ref the deck stores, not the resolved one: a deck whose template is missing resolves to
            // the default, and naming the default must still move it there.
            var moves = change.Template is not null && !string.Equals(change.Template, deck.Template, StringComparison.Ordinal);
            template = moves ? await PickAsync(change.Template!) : current;
            // The version the deck will be on, when the change names one. Every refusal comes before the first write.
            var versionMoves = false;
            if (change.TemplateVersion is { } wanted)
            {
                template = await VersionAsync(template, moves, deck.Template, wanted);
                versionMoves = !moves && template.Version != (deck.TemplateVersion ?? current.Version);
            }
            // The rules see the template the deck will be on, so the reserved palette name answers to that template's own.
            var catalogues = ResolvedCatalogues.For(palettes, template);
            var error = new LookRules(catalogues.Templates, catalogues.Palettes).CheckChange(template.Template, change with { Template = null });
            if (error is not null)
            {
                throw new BadRequestException(error);
            }
            if (change.Logo?.ImageId is { } imageId && !await images.ExistsAsync(deckId, imageId))
            {
                throw new BadRequestException(UnknownLogo(imageId));
            }

            // Naming the version the deck is already on, and nothing else, changes nothing: no revision, no entry.
            if (change is { Template: null, Palette: null, Logo: null, Draft: null, Chrome: null, Colors: null } && !versionMoves)
            {
                warnings = await WarningsAsync(deck.Id, template.Template);
                await transaction.CommitAsync();
                return (deck, template, warnings);
            }

            // A change that neither moves the deck nor names a palette leaves the stored palette alone, even one that
            // cannot be found while the deck's template is missing.
            var keepsPalette = !moves && !versionMoves && change.Palette is null && !template.Template.LockPalette;
            var palette = PaletteAfter(catalogues.Palettes, template, deck.Palette, change);
            var summary = LookSummary.Of(change, template.Template, palette, !keepsPalette && palette.Name != deck.Palette);
            if (change.Colors is { } colorChange)
            {
                var wording = colorChange.Value is { Count: > 0 } ? "set custom colours" : "cleared custom colours";
                summary = summary.Length == 0 ? wording : $"{summary}; {wording}";
            }
            if (versionMoves)
            {
                var wording = VersionMoveSummary.Of(template.Template.Label, deck.TemplateVersion ?? current.Version ?? template.Version!.Value, template.Version!.Value);
                summary = summary.Length == 0 ? wording : $"{wording}; {summary}";
            }
            if (moves || versionMoves)
            {
                deck.Template = template.Ref;
                deck.TemplateVersion = template.Version;
            }
            if (!keepsPalette)
            {
                deck.Palette = palette.Name;
            }

            if (change.Colors?.Value is { Count: > 0 } colors)
            {
                var refused = PaletteTokens.Check(palette, colors);
                if (refused is not null)
                {
                    throw new BadRequestException(refused);
                }
            }

            Apply(deck, change);
            await db.SaveChangesAsync();
            await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, tool, summary));
            warnings = await WarningsAsync(deck.Id, template.Template);
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deckId));
        return (deck, template, warnings);
    }

    // The template a change moves the deck to: only one the caller may pick is found (T4), so one that exists but is not
    // theirs is refused as unknown.
    private async Task<ResolvedTemplate> PickAsync(string name)
        => (await templates.ListForAsync(actor, CancellationToken.None)).FirstOrDefault(t => string.Equals(t.Ref, name, StringComparison.Ordinal))
            ?? throw new BadRequestException(LookRules.UnknownTemplate(name));

    // The deck's own template, for a version change that moves the deck nowhere. The caller may pick it (T4), or owns it: an
    // archived template leaves ListForAsync but its owner's decks keep rendering and may still move between versions (D7, the
    // rule UpdateMyDecks uses). Anyone else gets the unknown-template words, as for a revoked share.
    private async Task<ResolvedTemplate> PickCurrentAsync(string deckTemplate)
    {
        var listed = (await templates.ListForAsync(actor, CancellationToken.None)).FirstOrDefault(t => string.Equals(t.Ref, deckTemplate, StringComparison.Ordinal));
        if (listed is not null)
        {
            return listed;
        }
        if (actor.PresenterId is { } me && await db.Templates.AnyAsync(t => t.Id == deckTemplate && t.OwnerId == me))
        {
            return await templates.FindAsync(deckTemplate, null, CancellationToken.None) ?? throw new BadRequestException(LookRules.UnknownTemplate(deckTemplate));
        }
        throw new BadRequestException(LookRules.UnknownTemplate(deckTemplate));
    }

    // The template at the version a change names, for a deck that will be on an uploaded template. A template the caller may
    // not pick is refused as an unknown one first, even when it is the deck's own (T4: FindAsync is never the access check).
    private async Task<ResolvedTemplate> VersionAsync(ResolvedTemplate target, bool moves, string deckTemplate, TemplateVersionChange wanted)
    {
        if (target.BuiltIn)
        {
            throw new BadRequestException("templateVersion: built-in templates have no versions");
        }
        var pickable = moves ? target : await PickCurrentAsync(deckTemplate);
        if (wanted.Version is not { } number)
        {
            return pickable;
        }
        return await templates.FindAsync(pickable.Ref, number, CancellationToken.None)
            ?? throw new BadRequestException($"templateVersion: {pickable.Template.Label} has no version {number}");
    }

    // The palette the deck has after the change. A deck that leaves a template whose own palette it wore cannot keep it:
    // it takes the new template's palette, else the default one.
    private static Palette PaletteAfter(IPaletteCatalogue known, ResolvedTemplate template, string currentPalette, LookChange change)
    {
        var name = LookRules.PaletteAfter(template.Template, currentPalette, change);
        return known.Find(name) ?? known.Find(template.Template.Palette ?? known.Default.Name) ?? known.Default;
    }

    private void Apply(Deck deck, LookChange change)
    {
        if (change.Logo is not null)
        {
            deck.LogoImageId = change.Logo.ImageId;
        }
        if (change.Draft is { } draft)
        {
            deck.Draft = draft;
        }
        if (change.Colors is { } colors)
        {
            deck.PaletteTokensJson = PaletteTokens.Write(colors.Value);
        }

        if (change.Chrome is { } chrome)
        {
            var overrides = ChromeOverridesView.From(deck.ChromeJson);
            deck.ChromeJson = (overrides with
            {
                Tracker = chrome.Tracker is null ? overrides.Tracker : chrome.Tracker.Value,
                Footer = chrome.Footer is null ? overrides.Footer : chrome.Footer.Value,
                Progress = chrome.Progress is null ? overrides.Progress : chrome.Progress.Value,
                JoinBar = chrome.JoinBar ?? overrides.JoinBar,
                TopBar = chrome.TopBar is null ? overrides.TopBar : chrome.TopBar.Value,
            }).ToJson();
        }
        deck.Revision += 1;
        deck.UpdatedAt = clock.GetUtcNow();
    }

    private async Task<IReadOnlyList<string>> WarningsAsync(string deckId, Template template)
    {
        var layouts = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .Select(s => new { s.Position, s.Layout })
            .ToListAsync();
        return layouts.SelectMany(s => LayoutWarnings.For(template, s.Position + 1, s.Layout)).ToList();
    }
}
