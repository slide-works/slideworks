using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Sharing;

// TODO: only the owner (or an agent acting for the owner) may view or edit. The real access check replaces this with
// link sharing and named viewers for viewing; editing stays owner-only.
[RegisterPerRequest(For = typeof(IDeckAccess))]
public class OwnerOnlyDeckAccess(AppDbContext db) : IDeckAccess
{
    public Task<bool> CanViewAsync(string deckId, ICurrentActor actor) => IsOwnerAsync(deckId, actor);

    public Task<bool> CanEditAsync(string deckId, ICurrentActor actor) => IsOwnerAsync(deckId, actor);

    private async Task<bool> IsOwnerAsync(string deckId, ICurrentActor actor)
    {
        var presenterId = actor.PresenterId;
        return presenterId != null && await db.Decks.AnyAsync(d => d.Id == deckId && d.OwnerId == presenterId);
    }
}
