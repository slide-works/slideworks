using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks.Mcp;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Decks;

// The two reads every screen and tool builds on (K5): the play view for viewers and get_deck for the owner's agent.
// Deleted slides are never returned.
[RegisterPerRequest]
public class DeckReader(AppDbContext db, ICurrentActor actor, IDeckAccess access, IPublicUrls urls)
{
    public const string NOT_FOUND = "Deck not found";

    public async Task<DeckPlayView> GetPlayAsync(string deckId)
    {
        if (!await access.CanViewAsync(deckId, actor))
        {
            throw new NotFoundException(NOT_FOUND);
        }
        return DeckPlayView.From(await LoadDeckAsync(deckId), await LiveSlidesAsync(deckId));
    }

    // Only the owner, or an agent acting for the owner: the slides carry notes and revisions.
    public async Task<GetDeckResult> GetForOwnerAsync(string deckId)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(NOT_FOUND);
        }
        var deck = await LoadDeckAsync(deckId);
        return GetDeckResult.From(deck, await LiveSlidesAsync(deckId), urls.Deck(deck.Id, deck.Slug));
    }

    private Task<Deck> LoadDeckAsync(string deckId) => db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);

    private Task<List<Slide>> LiveSlidesAsync(string deckId)
        => db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .ToListAsync();
}
