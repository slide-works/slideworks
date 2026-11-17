using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Render;

// Which decks may name themselves to anyone (D12): only a deck whose sharing is link. The share card and the Open Graph
// tags ask this first, so an unknown, private or named-viewer deck never starts a render or reads its title.
[RegisterPerRequest]
public class LinkDecks(AppDbContext db)
{
    public Task<bool> IsLinkSharedAsync(string deckId)
        => db.Decks.AsNoTracking().AnyAsync(d => d.Id == deckId && d.Sharing == DeckSharing.Link);
}
