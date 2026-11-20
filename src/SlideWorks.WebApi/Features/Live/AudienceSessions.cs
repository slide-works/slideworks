using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Live;

// Finds an audience session by join code or by deck (J3), and is the only place the phase rule lives:
//   live   while a session is open;
//   ended  when the last session ended under ENDED_WINDOW ago;
//   lobby  otherwise, but only for a deck that has a join slide, has the join bar on, or has gone live before (D17).
// Any other code or deck gives null, so a walked code can't tell a private deck from no deck.
[RegisterPerRequest(For = typeof(IAudienceSessions))]
public class AudienceSessions(AppDbContext db, TimeProvider clock) : IAudienceSessions
{
    public static readonly TimeSpan ENDED_WINDOW = TimeSpan.FromHours(2);

    private const string JOIN_LAYOUT = "join";

    public async Task<AudienceSessionRef?> FindAsync(string code)
    {
        var deckId = await db.Decks.AsNoTracking().Where(d => d.JoinCode == code).Select(d => d.Id).SingleOrDefaultAsync();
        return deckId == null ? null : await ForDeckAsync(deckId);
    }

    // Live is an open session, as ForDeckAsync decides it.
    public Task<bool> AnyLiveAsync(IReadOnlyCollection<string> deckIds)
        => db.AudienceSessions.AsNoTracking().AnyAsync(s => s.EndedAt == null && deckIds.Contains(s.DeckId));

    public async Task<AudienceSessionRef?> ForDeckAsync(string deckId)
    {
        var deck = await db.Decks.AsNoTracking().Where(d => d.Id == deckId).Select(d => new { d.ChromeJson }).SingleOrDefaultAsync();
        if (deck == null)
        {
            return null;
        }
        var sessions = db.AudienceSessions.AsNoTracking().Where(s => s.DeckId == deckId);
        var open = await sessions.Where(s => s.EndedAt == null).Select(s => s.Id).SingleOrDefaultAsync();
        if (open != Guid.Empty)
        {
            return new AudienceSessionRef(deckId, open.ToString(), AudiencePhase.Live);
        }
        var lastEnded = await sessions.Where(s => s.EndedAt != null).OrderByDescending(s => s.EndedAt).Select(s => s.EndedAt).FirstOrDefaultAsync();
        if (lastEnded != null && lastEnded > clock.GetUtcNow() - ENDED_WINDOW)
        {
            return new AudienceSessionRef(deckId, null, AudiencePhase.Ended);
        }
        var hasLobby = lastEnded != null
            || ChromeOverridesView.From(deck.ChromeJson).JoinBar
            || await db.Slides.AsNoTracking().AnyAsync(s => s.DeckId == deckId && s.DeletedAt == null && s.Layout == JOIN_LAYOUT);
        return hasLobby ? new AudienceSessionRef(deckId, null, AudiencePhase.Lobby) : null;
    }
}
