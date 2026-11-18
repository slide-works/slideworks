using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Templates;

// A deck's look as the screens and tools show it (L6), behind one service so no caller holds a catalogue. A caller that
// already resolved the deck's template with ITemplates.ForDeckAsync passes it, so one request resolves it once.
public interface IDeckLooks
{
    // The deck's resolved look, for the player, the presenter view, the guest's phone and the card.
    Task<LookView> ForAsync(Deck deck, CancellationToken ct);

    // The same look, from the template the caller resolved for this deck.
    LookView Of(Deck deck, ResolvedTemplate template);

    // The deck page's look: the look plus the chrome overrides and the palette's contrast.
    Task<DeckLookView> ForPageAsync(Deck deck, CancellationToken ct);

    // The same deck page look, from the template the caller resolved for this deck.
    DeckLookView PageOf(Deck deck, ResolvedTemplate template);

    // The look of no deck in particular, for the generic share card: the default template and its palette.
    Task<LookView> DefaultAsync(DateTimeOffset now, CancellationToken ct);
}
