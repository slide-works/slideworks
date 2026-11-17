using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Templates;

// A deck's look as the screens and tools show it (L6), behind one service so no caller holds a catalogue.
public interface IDeckLooks
{
    // The deck's resolved look, for the player, the presenter view, the guest's phone and the card.
    Task<LookView> ForAsync(Deck deck, CancellationToken ct);

    // The deck page's look: the look plus the chrome overrides and the palette's contrast.
    Task<DeckLookView> ForPageAsync(Deck deck, CancellationToken ct);

    // The look of no deck in particular, for the generic share card: the default template and its palette.
    Task<LookView> DefaultAsync(DateTimeOffset now, CancellationToken ct);
}
