using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Presenting;

// The real IDeckAccess with a JoinGate in front of CanPresentAsync, so a test can hold a JoinPresentation inside the hub
// call. A fixture swaps it in with ConfigureTestServices.
public sealed class GatedDeckAccess(DeckAccess inner, JoinGate gate) : IDeckAccess
{
    public Task<bool> CanViewAsync(string deckId, ICurrentActor actor) => inner.CanViewAsync(deckId, actor);

    public Task<bool> CanEditAsync(string deckId, ICurrentActor actor) => inner.CanEditAsync(deckId, actor);

    public async Task<bool> CanPresentAsync(string deckId, ICurrentActor actor)
    {
        await gate.PassAsync(deckId);
        return await inner.CanPresentAsync(deckId, actor);
    }
}
