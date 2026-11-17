using Agoda.IoC.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Join;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Live;

// Go live, End and the live state (J2). Every check (who may see the deck, who owns it) runs before any write, so a refused
// call changes nothing. Go live is idempotent: it opens a session, or returns the one that is open.
[RegisterPerRequest]
public class LiveCommands(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IDeckEvents events,
    IAudienceRelay relay,
    IAudienceRoster roster,
    IAudienceSessions sessions,
    IPresentations presentations,
    IJoinUrls joinUrls,
    TimeProvider clock)
{
    // SQLITE_CONSTRAINT_UNIQUE: the extended code, so a NOT NULL or primary key failure is never mistaken for the race.
    private const int SQLITE_CONSTRAINT_UNIQUE = 2067;
    private const string OPEN_SESSION_INDEX = "IX_AudienceSessions_DeckId_Open";
    private const string OPEN_SESSION_COLUMN = "AudienceSessions.DeckId";

    public async Task<LiveView> GetAsync(string deckId)
    {
        var deck = await RequireOwnerAsync(deckId);
        // The phase rule lives in IAudienceSessions only: live is its answer, not a query of this class.
        var found = await sessions.ForDeckAsync(deckId);
        return await ViewAsync(deck, found?.Phase == AudiencePhase.Live ? Guid.Parse(found.SessionId!) : null);
    }

    public async Task<LiveView> GoLiveAsync(string deckId)
    {
        var deck = await RequireOwnerAsync(deckId);
        // Idempotent, so a repeat call also brings back a presentation state a restart dropped. It runs before the session
        // row is written, so a failure here leaves no open session behind.
        await presentations.StartAsync(deckId);
        var open = await OpenSessionAsync(deckId);
        var (sessionId, opened) = open == null ? await OpenAsync(deckId) : (open.Value, false);
        if (opened)
        {
            await roster.OpenedAsync(deckId, sessionId.ToString());
            await relay.StartedAsync(deckId);
            await activity.RecordAsync(deckId, ActivityEntry.By(actor, tool: null, "went live"));
            await events.PublishAsync(new LiveChanged(deckId));
        }
        return await ViewAsync(deck, sessionId);
    }

    public async Task EndAsync(string deckId)
    {
        await RequireOwnerAsync(deckId);
        var session = await db.AudienceSessions.SingleOrDefaultAsync(s => s.DeckId == deckId && s.EndedAt == null);
        if (session == null)
        {
            return;
        }
        session.EndedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        await roster.ClosedAsync(deckId);
        await relay.EndedAsync(deckId);
        await activity.RecordAsync(deckId, ActivityEntry.By(actor, tool: null, "ended live session"));
        await events.PublishAsync(new LiveChanged(deckId));
    }

    // 404 for a deck the caller can't see, 403 for one they can see but don't own (SW-4 D3).
    private async Task<Deck> RequireOwnerAsync(string deckId)
    {
        if (!await access.CanViewAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (!await access.CanPresentAsync(deckId, actor))
        {
            throw new ForbiddenException("Only the deck's owner can go live");
        }
        return await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);
    }

    private async Task<Guid?> OpenSessionAsync(string deckId)
        => await db.AudienceSessions.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.EndedAt == null)
            .Select(s => (Guid?)s.Id)
            .SingleOrDefaultAsync();

    // Opens a session. When another Go live won the race, the partial unique index on open sessions throws, and the session
    // that call opened is the one returned.
    private async Task<(Guid Id, bool Created)> OpenAsync(string deckId)
    {
        var session = new AudienceSession { Id = Guid.NewGuid(), DeckId = deckId, StartedAt = clock.GetUtcNow() };
        db.AudienceSessions.Add(session);
        try
        {
            await db.SaveChangesAsync();
            return (session.Id, true);
        }
        catch (DbUpdateException ex) when (IsOpenSessionConflict(ex))
        {
            db.Entry(session).State = EntityState.Detached;
            var open = await OpenSessionAsync(deckId) ?? throw new InvalidOperationException($"Deck {deckId} has no open session after a conflict.");
            return (open, false);
        }
    }

    private static bool IsOpenSessionConflict(DbUpdateException ex)
        => ex.InnerException is SqliteException { SqliteExtendedErrorCode: SQLITE_CONSTRAINT_UNIQUE } inner
            && (inner.Message.Contains(OPEN_SESSION_COLUMN, StringComparison.Ordinal) || inner.Message.Contains(OPEN_SESSION_INDEX, StringComparison.Ordinal));

    private async Task<LiveView> ViewAsync(Deck deck, Guid? sessionId)
    {
        var live = sessionId != null;
        return new LiveView(
            sessionId?.ToString(),
            deck.JoinCode,
            JoinCodes.Display(deck.JoinCode),
            joinUrls.JoinUrl(deck.JoinCode),
            joinUrls.HostLabel(),
            live,
            live ? await roster.CountAsync(sessionId!.Value.ToString()) : 0,
            ChromeOverridesView.From(deck.ChromeJson).JoinBar);
    }
}
