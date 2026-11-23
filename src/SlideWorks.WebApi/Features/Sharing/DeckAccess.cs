using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Sharing;

// K8. The owner may view and edit; an agent acts as the presenter it works for. A deck shared by link may be viewed by
// anyone, signed in or not. A deck shared with named people may be viewed by a signed-in person whose email is on its
// list, compared after ViewerEmails.Normalize. A pending person counts (SW-54 D2): the presenter vouched for them by
// naming them, and approval gates making decks, not opening one shared with you. Editing stays owner-only. An unknown
// deck is false for both, so callers answer it exactly as a forbidden one.
[RegisterPerRequest(For = typeof(IDeckAccess))]
public class DeckAccess(AppDbContext db) : IDeckAccess
{
    public async Task<bool> CanViewAsync(string deckId, ICurrentActor actor)
    {
        var deck = await db.Decks.AsNoTracking()
            .Where(d => d.Id == deckId)
            .Select(d => new { d.OwnerId, d.Sharing })
            .SingleOrDefaultAsync();
        if (deck == null)
        {
            return false;
        }
        if (deck.OwnerId == actor.PresenterId || deck.Sharing == DeckSharing.Link)
        {
            return true;
        }
        if (deck.Sharing != DeckSharing.Named || !actor.IsSignedIn || string.IsNullOrWhiteSpace(actor.Email))
        {
            return false;
        }
        var email = ViewerEmails.Normalize(actor.Email);
        return await db.DeckViewers.AnyAsync(v => v.DeckId == deckId && v.Email == email);
    }

    public async Task<bool> CanEditAsync(string deckId, ICurrentActor actor) => await IsOwnerAsync(deckId, actor);

    public async Task<bool> IsOwnerAsync(string deckId, ICurrentActor actor)
    {
        var presenterId = actor.PresenterId;
        return presenterId != null && await db.Decks.AnyAsync(d => d.Id == deckId && d.OwnerId == presenterId);
    }

    public async Task<bool> CanPresentAsync(string deckId, ICurrentActor actor)
        => actor.Kind == ActorKind.Person && await CanEditAsync(deckId, actor);
}
