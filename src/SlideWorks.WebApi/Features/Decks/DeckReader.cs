using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks.Mcp;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Decks;

// The two reads every screen and tool builds on (K5): the play view for viewers and get_deck for the owner's agent.
// Deleted slides are never returned.
[RegisterPerRequest]
public class DeckReader(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IPublicUrls urls,
    JoinViews joinViews,
    IDeckLooks looks,
    ITemplates templates)
{
    public const string NOT_FOUND = "Deck not found";

    public async Task<DeckPlayView> GetPlayAsync(string deckId)
    {
        if (!await access.CanViewAsync(deckId, actor))
        {
            throw new NotFoundException(NOT_FOUND);
        }
        return await PlayAsync(await LoadDeckAsync(deckId), await access.CanPresentAsync(deckId, actor));
    }

    // For the render API (behind a render token) and the card cache only (L9): the play view with the deck's revision
    // and sharing, and no access check. An unknown deck is 404 "Deck not found".
    public async Task<DeckForRender> ReadForRenderAsync(string deckId)
    {
        var deck = await db.Decks.AsNoTracking().SingleOrDefaultAsync(d => d.Id == deckId) ?? throw new NotFoundException(NOT_FOUND);
        return new DeckForRender(await PlayAsync(deck, isOwner: false), deck.Subtitle, deck.Revision, deck.Sharing);
    }

    // Only the owner, or an agent acting for the owner: the slides carry notes and revisions.
    public async Task<GetDeckResult> GetForOwnerAsync(string deckId)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(NOT_FOUND);
        }
        var deck = await LoadDeckAsync(deckId);
        return GetDeckResult.From(deck, await LiveSlidesAsync(deckId), await templates.ForDeckAsync(deck, CancellationToken.None), urls.Deck(deck.Id, deck.Slug));
    }

    private async Task<DeckPlayView> PlayAsync(Deck deck, bool isOwner)
        => DeckPlayView.From(deck, await LiveSlidesAsync(deck.Id), await templates.ForDeckAsync(deck, CancellationToken.None), await looks.ForAsync(deck, CancellationToken.None), await joinViews.ForAsync(deck), isOwner);

    private Task<Deck> LoadDeckAsync(string deckId) => db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);

    private Task<List<Slide>> LiveSlidesAsync(string deckId)
        => db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .ToListAsync();
}
