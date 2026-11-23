using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.ConnectedAgents;

// One agent per live token family: a family with an access or refresh token that is not revoked, not expired and not
// already rotated. Dates are folded in memory because SQLite cannot aggregate a DateTimeOffset.
[RegisterPerRequest]
public sealed class EfConnectedAgents(AppDbContext db, TimeProvider clock, IDisplayDates dates) : IConnectedAgents
{
    public async Task<IReadOnlyList<ConnectedAgent>> ListAsync(Guid presenterId)
    {
        var now = clock.GetUtcNow();
        var rows = await (
                from token in db.AgentTokens.AsNoTracking()
                join client in db.OAuthClients.AsNoTracking() on token.ClientId equals client.ClientId
                where token.PresenterId == presenterId && token.RevokedAt == null && token.UsedAt == null && token.ExpiresAt > now
                select new { token.FamilyId, client.ClientName, token.IssuedAt, token.LastUsedAt })
            .ToListAsync();
        return
        [
            .. rows
                .GroupBy(r => r.FamilyId)
                .Select(g => new
                {
                    FamilyId = g.Key,
                    g.First().ClientName,
                    IssuedAt = g.Min(r => r.IssuedAt),
                    LastUsedAt = g.Max(r => r.LastUsedAt),
                })
                .OrderByDescending(a => a.IssuedAt)
                .ThenBy(a => a.FamilyId)
                .Select(a => new ConnectedAgent(
                    a.FamilyId,
                    a.ClientName,
                    a.IssuedAt is { } issued ? dates.Ago(issued) : "Unknown",
                    a.LastUsedAt is { } used ? dates.Ago(used) : null)),
        ];
    }

    // Revokes the whole family in one statement, and only while it is this person's and live.
    public async Task<bool> DisconnectAsync(Guid presenterId, Guid familyId)
    {
        var now = clock.GetUtcNow();
        var revoked = await db.AgentTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null
                && db.AgentTokens.Any(live => live.FamilyId == familyId
                    && live.PresenterId == presenterId
                    && live.RevokedAt == null
                    && live.UsedAt == null
                    && live.ExpiresAt > now))
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now));
        return revoked > 0;
    }
}
