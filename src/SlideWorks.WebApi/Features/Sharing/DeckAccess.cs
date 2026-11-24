using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Sharing;

// K8, C5. The owner may view, edit and present; an agent acts as the presenter it works for. A deck shared by link may be
// viewed by anyone, signed in or not. A person on the deck's list (read or write, compared after ViewerEmails.Normalize)
// may view it at level named or link, and edit it with write at those levels, as a person or through their own agent. At
// level private only the owner gets in, people with write included; their rows are kept (D12). A pending person counts
// (SW-54 D2): approval gates making decks, not opening one shared with you. Only IsOwnerAsync gates sharing, the title,
// deleting and transferring (D7). An unknown deck is false for all, so callers answer it exactly as a forbidden one.
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
        return deck.Sharing == DeckSharing.Named && await IsOnListAsync(deckId, actor, writeOnly: false);
    }

    public async Task<bool> CanEditAsync(string deckId, ICurrentActor actor)
    {
        var deck = await db.Decks.AsNoTracking()
            .Where(d => d.Id == deckId)
            .Select(d => new { d.OwnerId, d.Sharing })
            .SingleOrDefaultAsync();
        if (deck == null)
        {
            return false;
        }
        if (actor.PresenterId != null && deck.OwnerId == actor.PresenterId)
        {
            return true;
        }
        return deck.Sharing != DeckSharing.Private && await IsOnListAsync(deckId, actor, writeOnly: true);
    }

    public async Task<bool> IsOwnerAsync(string deckId, ICurrentActor actor)
    {
        var presenterId = actor.PresenterId;
        return presenterId != null && await db.Decks.AnyAsync(d => d.Id == deckId && d.OwnerId == presenterId);
    }

    public async Task<bool> CanPresentAsync(string deckId, ICurrentActor actor)
        => actor.Kind == ActorKind.Person && await CanEditAsync(deckId, actor);

    private async Task<bool> IsOnListAsync(string deckId, ICurrentActor actor, bool writeOnly)
    {
        if (!actor.IsSignedIn || string.IsNullOrWhiteSpace(actor.Email))
        {
            return false;
        }
        var email = ViewerEmails.Normalize(actor.Email);
        return await db.DeckViewers.AnyAsync(v => v.DeckId == deckId && v.Email == email
            && (!writeOnly || v.Access == DeckViewerAccess.Write));
    }
}
