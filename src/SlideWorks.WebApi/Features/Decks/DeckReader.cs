using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks.Mcp;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Slides;
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
    ITemplateCatalogue templates,
    IPaletteCatalogue palettes,
    QaRows qa)
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
        var slides = await LiveSlidesAsync(deckId);
        return GetDeckResult.From(deck, slides, urls.Deck(deck.Id, deck.Slug), await qa.SettingsAsync(deckId), await ReactionsAsync(slides));
    }

    // Each session's reaction totals per slide of this deck, newest session first. The totals are rows of the Q&A feature,
    // read here as counts only.
    private async Task<IReadOnlyList<GetDeckReactionSession>> ReactionsAsync(IReadOnlyList<Slide> slides)
    {
        var numbers = slides.ToDictionary(s => s.Id, s => s.Position + 1);
        var ids = numbers.Keys.ToList();
        var totals = await db.ReactionTotals.AsNoTracking().Where(t => ids.Contains(t.SlideId)).ToListAsync();
        var sessionIds = totals.Select(t => t.SessionId).Distinct().ToList();
        var starts = await db.AudienceSessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id)).Select(s => new { s.Id, s.StartedAt }).ToListAsync();
        return [.. starts.OrderByDescending(s => s.StartedAt).Select(session => new GetDeckReactionSession(
            session.StartedAt,
            [.. totals.Where(t => t.SessionId == session.Id).GroupBy(t => numbers[t.SlideId]).OrderBy(g => g.Key).Select(g => new GetDeckReactionSlide(g.Key, CountsOf(g)))]))];
    }

    private static ReactionCounts CountsOf(IEnumerable<ReactionTotal> totals)
    {
        var count = (ReactionEmoji emoji) => totals.Where(t => t.Emoji == emoji).Sum(t => t.Count);
        return new ReactionCounts(count(ReactionEmoji.ThumbsUp), count(ReactionEmoji.Heart), count(ReactionEmoji.Laugh), count(ReactionEmoji.Wow), count(ReactionEmoji.Clap));
    }

    private async Task<DeckPlayView> PlayAsync(Deck deck, bool isOwner)
        => DeckPlayView.From(deck, await LiveSlidesAsync(deck.Id), LookViews.From(deck, templates, palettes), await joinViews.ForAsync(deck), isOwner);

    private Task<Deck> LoadDeckAsync(string deckId) => db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);

    private Task<List<Slide>> LiveSlidesAsync(string deckId)
        => db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .ToListAsync();
}
