using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Features.Decks.Mcp;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Decks;

// The presenter's reads: the decks library and the deck page (K4), and the list_decks and get_deck_url tools (K6). Every
// read is an AsNoTracking projection, deleted slides never count or show, and time labels are formatted here from
// TimeProvider. The deck page is for the owner and for people with write access (C4, C5): anyone else, readers included,
// gets 404 "Deck not found".
[RegisterPerRequest]
public class DeckQueries(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IPublicUrls urls,
    JoinViews joinViews,
    IDeckLooks looks,
    ITemplates templates,
    IStoredSlideChecks storedChecks,
    IDisplayDates dates)
{
    public const int ACTIVITY_MAX = 50;

    public async Task<DecksLibraryView> LibraryAsync()
    {
        var presenterId = PresenterId();
        var connected = await db.AgentTokens.AsNoTracking().AnyAsync(t => t.PresenterId == presenterId && t.RevokedAt == null);
        return new DecksLibraryView(await OwnDecksAsync(presenterId), await SharedWithMeAsync(presenterId), new ConnectAgent(!connected, urls.Mcp));
    }

    public async Task<DeckPageView> PageAsync(string deckId)
    {
        await RequireEditorAsync(deckId);
        var isOwner = await access.IsOwnerAsync(deckId, actor);
        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);
        var slides = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .ToListAsync();
        var viewerCount = await db.DeckViewers.AsNoTracking().CountAsync(v => v.DeckId == deckId);
        var entries = await activity.ListAsync(deckId, ACTIVITY_MAX);
        var template = await templates.ForDeckAsync(deck, CancellationToken.None);
        var findings = await storedChecks.CheckAsync(deckId, [.. Enumerable.Range(1, slides.Count)], CancellationToken.None);
        return new DeckPageView(
            deck.Id,
            deck.Slug,
            deck.Title,
            deck.Author,
            deck.Revision,
            IsOwner: isOwner,
            CanEdit: true,
            Access: isOwner ? "owner" : "write",
            DeckLinks.Play(deck.Id, deck.Slug),
            await joinViews.ForAsync(deck),
            SharingSummary.From(deck.Sharing, viewerCount),
            DeckParts.Read(deck),
            looks.PageOf(deck, template),
            slides.Select(slide => DeckPageSlide.From(deck, slide, template)).ToList(),
            entries.Select(entry => ActivityRow.From(entry, dates.Ago(entry.At), isOwner)).ToList(),
            findings);
    }

    // list_decks: the decks the agent's presenter owns, newest change first, then the decks shared with them with write
    // access (access "write", newest change first), with absolute player links. Read shares are not listed (C4), and a
    // deck at level private is not listed for a person with write (C5, D12).
    public async Task<ListDecksResult> ListForAgentAsync()
    {
        var presenterId = PresenterId();
        var own = (await OwnDecksAsync(presenterId)).Select(row => ListedDeck.From(row, urls.Deck(row.Id, row.Slug), "owner"));
        var shared = (await SharedWithMeAsync(presenterId)).Where(row => row.Access == "write")
            .Select(row => ListedDeck.From(
                new DeckRow(row.Id, row.Slug, row.Title, row.Subtitle, row.SlideCount, row.LastChange, row.Sharing, row.ViewerCount, row.UpdatedLabel, row.PlayUrl),
                urls.Deck(row.Id, row.Slug),
                row.Access));
        return new(own.Concat(shared).ToList());
    }

    // get_deck_url: the absolute player link, to one slide (#n) or one step of it (#n.s) when given. Slide is 1-based
    // among the live slides; step runs from 0 to the slide's steps.
    public async Task<GetDeckUrlResult> UrlAsync(string deckId, int? slide, int? step)
    {
        await RequireEditorAsync(deckId);
        var deck = await db.Decks.AsNoTracking().Where(d => d.Id == deckId).Select(d => new { d.Id, d.Slug }).SingleAsync();
        if (slide == null)
        {
            return step == null
                ? new GetDeckUrlResult(urls.Deck(deck.Id, deck.Slug))
                : throw new BadRequestException("step: needs a slide");
        }
        var live = db.Slides.AsNoTracking().Where(s => s.DeckId == deckId && s.DeletedAt == null);
        var steps = await live.Where(s => s.Position == slide - 1).Select(s => (int?)s.Steps).SingleOrDefaultAsync();
        if (steps == null)
        {
            throw new BadRequestException($"slide: no slide {slide}; the deck has {await live.CountAsync()}");
        }
        if (step < 0 || step > steps)
        {
            throw new BadRequestException($"step: slide {slide} has steps 0 to {steps}");
        }
        return new GetDeckUrlResult(urls.Deck(deck.Id, deck.Slug, slide, step));
    }

    private Guid PresenterId() => actor.PresenterId ?? throw new InvalidOperationException("Reading decks needs a signed-in caller.");

    private async Task RequireEditorAsync(string deckId)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }

    private async Task<IReadOnlyList<DeckRow>> OwnDecksAsync(Guid presenterId)
    {
        var decks = await db.Decks.AsNoTracking()
            .Where(d => d.OwnerId == presenterId)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new
            {
                d.Id,
                d.Slug,
                d.Title,
                d.Subtitle,
                d.Sharing,
                d.UpdatedAt,
                SlideCount = db.Slides.Count(s => s.DeckId == d.Id && s.DeletedAt == null),
                ViewerCount = db.DeckViewers.Count(v => v.DeckId == d.Id),
                LastChange = db.ActivityEntries.Where(e => e.DeckId == d.Id).OrderByDescending(e => e.At).ThenByDescending(e => e.Id).FirstOrDefault(),
            })
            .ToListAsync();
        return decks
            .Select(d => new DeckRow(
                d.Id,
                d.Slug,
                d.Title,
                d.Subtitle,
                d.SlideCount,
                d.LastChange == null ? null : LastChange.From(d.LastChange, isOwner: true),
                d.Sharing,
                d.ViewerCount,
                dates.Ago(d.UpdatedAt),
                DeckLinks.Play(d.Id, d.Slug)))
            .ToList();
    }

    // Decks that someone else owns and that list the caller's email, compared case-insensitively, at level named or link
    // (C5: at private the list is kept but the deck is paused for them, D12). Each row carries the caller's access and
    // links to the player.
    private async Task<IReadOnlyList<SharedDeckRow>> SharedWithMeAsync(Guid presenterId)
    {
        var email = actor.Email?.ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
        {
            return [];
        }
        var decks = await db.Decks.AsNoTracking()
            .Where(d => d.Sharing != DeckSharing.Private
                && d.OwnerId != presenterId
                && db.DeckViewers.Any(v => v.DeckId == d.Id && v.Email.ToLower() == email))
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new
            {
                d.Id,
                d.Slug,
                d.Title,
                d.Subtitle,
                d.Sharing,
                d.UpdatedAt,
                Write = db.DeckViewers.Any(v => v.DeckId == d.Id && v.Email.ToLower() == email && v.Access == DeckViewerAccess.Write),
                OwnerName = db.Users.Where(u => u.Id == d.OwnerId).Select(u => u.Name).Single(),
                SlideCount = db.Slides.Count(s => s.DeckId == d.Id && s.DeletedAt == null),
                ViewerCount = db.DeckViewers.Count(v => v.DeckId == d.Id),
                LastChange = db.ActivityEntries.Where(e => e.DeckId == d.Id).OrderByDescending(e => e.At).ThenByDescending(e => e.Id).FirstOrDefault(),
            })
            .ToListAsync();
        return decks
            .Select(d => SharedDeckRow.From(
                new DeckRow(
                    d.Id,
                    d.Slug,
                    d.Title,
                    d.Subtitle,
                    d.SlideCount,
                    d.LastChange == null ? null : LastChange.From(d.LastChange, isOwner: false),
                    d.Sharing,
                    d.ViewerCount,
                    dates.Ago(d.UpdatedAt),
                    DeckLinks.Play(d.Id, d.Slug)),
                d.OwnerName,
                d.Write ? "write" : "read"))
            .ToList();
    }
}
