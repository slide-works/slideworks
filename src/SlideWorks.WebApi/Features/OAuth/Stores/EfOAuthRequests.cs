using System.Linq.Expressions;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Authorize requests waiting for consent, one OAuthRequestRow each. The request id is stored as it is, not hashed: it
// only identifies the request, and consuming it also needs the presenter it was made for, so a leaked id alone gets
// nobody a code.
[RegisterPerRequest(For = typeof(IOAuthRequests))]
public class EfOAuthRequests(AppDbContext db, TimeProvider clock) : IOAuthRequests
{
    public async Task<string> CreateAsync(PendingAuthorization pending)
    {
        var row = new OAuthRequestRow
        {
            Id = SecretTokens.New(),
            ClientId = pending.ClientId,
            PresenterId = pending.PresenterId,
            RedirectUri = pending.RedirectUri,
            State = pending.State,
            CodeChallenge = pending.CodeChallenge,
            Scope = pending.Scope,
            Resource = pending.Resource,
            ExpiresAt = clock.GetUtcNow() + OAuthLifetimes.Request,
        };
        db.OAuthRequests.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public async Task<PendingAuthorization?> FindAsync(string requestId, Guid presenterId)
    {
        var row = await db.OAuthRequests.AsNoTracking().Where(Open(requestId, presenterId, clock.GetUtcNow())).SingleOrDefaultAsync();
        return row == null ? null : ToPending(row);
    }

    // One conditional update consumes the request: of any number of parallel attempts, exactly one changes the row.
    // Another presenter's attempt matches no row, so it leaves the request as it was.
    public async Task<PendingAuthorization?> ConsumeAsync(string requestId, Guid presenterId)
    {
        var now = clock.GetUtcNow();
        var consumed = await db.OAuthRequests
            .Where(Open(requestId, presenterId, now))
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.ConsumedAt, now));
        if (consumed != 1)
        {
            return null;
        }
        var row = await db.OAuthRequests.AsNoTracking().SingleAsync(r => r.Id == requestId);
        return ToPending(row);
    }

    // The one rule for a request that can still be shown or answered: this id, this presenter's, not consumed, not expired.
    private static Expression<Func<OAuthRequestRow, bool>> Open(string requestId, Guid presenterId, DateTimeOffset now)
        => r => r.Id == requestId && r.PresenterId == presenterId && r.ConsumedAt == null && r.ExpiresAt > now;

    private static PendingAuthorization ToPending(OAuthRequestRow row)
        => new(row.ClientId, row.PresenterId, row.RedirectUri, row.State, row.CodeChallenge, row.Scope, row.Resource);
}
