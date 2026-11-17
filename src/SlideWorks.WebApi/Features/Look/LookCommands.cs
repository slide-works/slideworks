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
[RegisterPerRequest]
public class LookCommands(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IDeckImages images,
    IActivityLog activity,
    IDeckEvents events,
    TimeProvider clock,
    ITemplateCatalogue templates,
    IPaletteCatalogue palettes)
{
    public const string TOOL = "set_look";
    public const string NOTHING_TO_CHANGE = "Nothing to change";

    // tool is the MCP tool's name when an agent calls, for the change log, and null from the web.
    public async Task<LookChangeResult> SetAsync(string deckId, LookChange change, string? tool)
    {
        var (deck, warnings) = await ChangeAsync(deckId, change, tool);
        return new LookChangeResult(LookViews.ForPage(deck, templates, palettes), warnings);
    }

    // set_look: the same change, with the look in get_deck's terms.
    public async Task<SetLookResult> SetFromAgentAsync(string deckId, LookChange change)
    {
        var (deck, warnings) = await ChangeAsync(deckId, change, TOOL);
        return new SetLookResult(GetDeckLook.From(deck), warnings);
    }

    public static string UnknownLogo(string imageId) => $"logo.image: unknown image {imageId}";

    private async Task<(Deck Deck, IReadOnlyList<string> Warnings)> ChangeAsync(string deckId, LookChange change, string? tool)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (change is { Template: null, Palette: null, Logo: null, Draft: null, Chrome: null })
        {
            throw new BadRequestException(NOTHING_TO_CHANGE);
        }

        Deck deck;
        IReadOnlyList<string> warnings;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            deck = await db.Decks.SingleAsync(d => d.Id == deckId);
            var current = templates.Find(deck.Template) ?? throw new InvalidOperationException($"Deck {deck.Id} has an unknown template '{deck.Template}'.");
            var error = new LookRules(templates, palettes).CheckChange(current, change);
            if (error is not null)
            {
                throw new BadRequestException(error);
            }
            if (change.Logo?.ImageId is { } imageId && !await images.ExistsAsync(deckId, imageId))
            {
                throw new BadRequestException(UnknownLogo(imageId));
            }

            var template = change.Template is null ? current : templates.Find(change.Template)!;
            var palette = palettes.Find(LookRules.PaletteAfter(template, deck.Palette, change))!;
            var summary = LookSummary.Of(change, template, palette, palette.Name != deck.Palette);
            Apply(deck, change, template, palette);
            await db.SaveChangesAsync();
            await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, tool, summary));
            warnings = await WarningsAsync(deck.Id, template);
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deckId));
        return (deck, warnings);
    }

    private void Apply(Deck deck, LookChange change, Template template, Palette palette)
    {
        deck.Template = template.Name;
        deck.Palette = palette.Name;
        if (change.Logo is not null)
        {
            deck.LogoImageId = change.Logo.ImageId;
        }
        if (change.Draft is { } draft)
        {
            deck.Draft = draft;
        }
        if (change.Chrome is { } chrome)
        {
            var overrides = ChromeOverridesView.From(deck.ChromeJson);
            deck.ChromeJson = new ChromeOverridesView(
                chrome.Tracker is null ? overrides.Tracker : chrome.Tracker.Value,
                chrome.Footer is null ? overrides.Footer : chrome.Footer.Value,
                chrome.Progress is null ? overrides.Progress : chrome.Progress.Value,
                chrome.JoinBar ?? overrides.JoinBar).ToJson();
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
