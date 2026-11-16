using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Activity;

[RegisterPerRequest(For = typeof(IActivityLog))]
public class EfActivityLog(AppDbContext db, TimeProvider clock) : IActivityLog
{
    public async Task RecordAsync(string deckId, ActivityEntry entry)
    {
        db.ActivityEntries.Add(new ActivityEntryRow
        {
            DeckId = deckId,
            ActorName = entry.ActorName,
            ActorKind = entry.ActorKind.ToString(),
            Via = entry.Via.ToString(),
            Tool = entry.Tool,
            Summary = entry.Summary,
            At = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ActivityEntryRow>> ListAsync(string deckId, int max)
        => await db.ActivityEntries.AsNoTracking()
            .Where(e => e.DeckId == deckId)
            .OrderByDescending(e => e.At)
            .ThenByDescending(e => e.Id)
            .Take(max)
            .ToListAsync();
}
