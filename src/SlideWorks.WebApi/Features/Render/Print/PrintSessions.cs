using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Render.Print;

// The deck's last talk: its audience session with the latest StartedAt, live or ended.
[RegisterPerRequest]
public class PrintSessions(AppDbContext db)
{
    public async Task<Guid?> NewestAsync(string deckId, CancellationToken ct) =>
        await db.AudienceSessions.AsNoTracking()
            .Where(session => session.DeckId == deckId)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .Select(session => (Guid?)session.Id)
            .FirstOrDefaultAsync(ct);
}
