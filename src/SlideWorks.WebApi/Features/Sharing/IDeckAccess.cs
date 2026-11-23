using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Sharing;

// Who may open and who may change a deck (K8). Both are false for an unknown deck, so a caller can't tell an unknown deck
// from a forbidden one: each answers 404 "Deck not found". An agent acts as its presenter.
public interface IDeckAccess
{
    Task<bool> CanViewAsync(string deckId, ICurrentActor actor);

    Task<bool> CanEditAsync(string deckId, ICurrentActor actor);

    // The deck's owner, as a person or through their agent (C5). Changing sharing, the title, deleting and transferring
    // use it, so a person with write access never passes. False for an unknown deck.
    Task<bool> IsOwnerAsync(string deckId, ICurrentActor actor);

    // Who may present the deck and see its notes (SW-4 D3, P2, P3): its owner, signed in as a person. An agent acting for
    // the owner and every viewer are false.
    Task<bool> CanPresentAsync(string deckId, ICurrentActor actor);
}
