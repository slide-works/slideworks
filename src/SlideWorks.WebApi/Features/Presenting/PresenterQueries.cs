using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Presenting.Views;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Presenting;

// The presenter's reads (SW-4): the presenter view, the presenter window's and the phone's one request (P3), and the
// signed-in person's live presentations (P4). The presenter view carries notes, so only the deck's owner, signed in as a
// person, gets it (IDeckAccess.CanPresentAsync, P9); anyone else gets 404 "Deck not found", as for an unknown deck.
[RegisterPerRequest]
public class PresenterQueries(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IPublicUrls urls,
    JoinViews joinViews,
    IDeckLooks looks,
    ITemplates templates,
    IPresentations presentations)
{
    public async Task<PresenterView> PresenterAsync(string deckId)
    {
        if (!await access.CanPresentAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);
        var slides = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .ToListAsync();
        return PresenterView.From(deck, slides, await templates.ForDeckAsync(deck, CancellationToken.None), await looks.ForAsync(deck, CancellationToken.None), await joinViews.ForAsync(deck), urls);
    }

    public Task<LivePresentationsView> LiveAsync()
        => presentations.ListLiveAsync(actor.PresenterId ?? throw new InvalidOperationException("Listing presentations needs a signed-in caller."));
}
