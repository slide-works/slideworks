using Agoda.IoC.Core;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// IDeckLooks over the template seam (T4): the deck's template comes from ITemplates, so an uploaded one reaches every
// screen. A deck always renders (D3): when its template cannot be resolved the default stands in, never silently: a
// warning names the deck and the ref, and the look carries templateMissing.
[RegisterPerRequest(For = typeof(IDeckLooks))]
public sealed class DeckLooks(ITemplates templates, IPaletteCatalogue palettes, ICurrentActor actor, ILogger<DeckLooks> logger) : IDeckLooks
{
    public async Task<LookView> ForAsync(Deck deck, CancellationToken ct) => Of(deck, await templates.ForDeckAsync(deck, ct));

    public LookView Of(Deck deck, ResolvedTemplate template)
    {
        WarnWhenMissing(deck, template);
        return LookViews.From(deck, template, palettes);
    }

    public async Task<DeckLookView> ForPageAsync(Deck deck, CancellationToken ct) => PageOf(deck, await templates.ForDeckAsync(deck, ct));

    public DeckLookView PageOf(Deck deck, ResolvedTemplate template)
    {
        WarnWhenMissing(deck, template);
        return LookViews.ForPage(deck, template, palettes);
    }

    // The default is the default built-in, as for a new deck that names no template.
    public async Task<LookView> DefaultAsync(DateTimeOffset now, CancellationToken ct)
    {
        var pickable = await templates.ListForAsync(actor, ct);
        return LookViews.Default(pickable.Where(t => t.Template.IsDefault).OrderByDescending(t => t.BuiltIn).First(), palettes, now);
    }

    private void WarnWhenMissing(Deck deck, ResolvedTemplate template)
    {
        if (LookViews.IsMissing(deck, template))
        {
            logger.LogWarning(
                "Deck {DeckId} names template {TemplateRef} version {TemplateVersion}, which cannot be resolved; it renders with {Fallback}",
                deck.Id,
                deck.Template,
                deck.TemplateVersion,
                template.Ref);
        }
    }
}
