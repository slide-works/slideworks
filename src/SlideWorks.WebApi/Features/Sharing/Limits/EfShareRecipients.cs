using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Sharing.Limits;

[RegisterPerRequest(For = typeof(IShareRecipients))]
public class EfShareRecipients(AppDbContext db, TimeProvider clock) : IShareRecipients
{
    public async Task RecordAsync(Guid sharerId, string deckId, IReadOnlyList<string> emails)
    {
        if (emails.Count == 0)
        {
            return;
        }
        var now = clock.GetUtcNow();
        foreach (var email in emails.Select(ViewerEmails.Normalize).Distinct(StringComparer.Ordinal))
        {
            db.ShareRecipients.Add(new ShareRecipientRow { Id = Guid.NewGuid(), SharerId = sharerId, DeckId = deckId, Email = email, AddedAt = now });
        }
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<RecentRecipient>> DistinctSinceAsync(Guid sharerId, DateTimeOffset since)
    {
        var rows = await db.ShareRecipients
            .Where(r => r.SharerId == sharerId && r.AddedAt >= since)
            .Select(r => new { r.Email, r.AddedAt })
            .ToListAsync();
        return rows
            .GroupBy(r => r.Email, StringComparer.Ordinal)
            .Select(g => new RecentRecipient(g.Key, g.Max(r => r.AddedAt)))
            .OrderBy(r => r.LastAddedAt)
            .ThenBy(r => r.Email, StringComparer.Ordinal)
            .ToList();
    }
}
