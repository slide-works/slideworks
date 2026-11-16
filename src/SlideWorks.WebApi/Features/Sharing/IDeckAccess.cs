using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Sharing;

// Who may open and who may change a deck (K8). Both are false for an unknown deck, so a caller can't tell an unknown deck
// from a forbidden one: each answers 404 "Deck not found". An agent acts as its presenter.
public interface IDeckAccess
{
    Task<bool> CanViewAsync(string deckId, ICurrentActor actor);

    Task<bool> CanEditAsync(string deckId, ICurrentActor actor);
}
