using Agoda.IoC.Core;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Access and refresh tokens, one AgentTokenRow each, found by the token's SecretTokens.Hash. The token itself is never
// stored. A rotation is one conditional update inside a write transaction: SQLite's BEGIN IMMEDIATE serialises it with
// any other rotation or revoke, so of parallel attempts exactly one marks the token used, and every later one sees that.
[RegisterPerRequest(For = typeof(IAgentTokens))]
public class EfAgentTokens(AppDbContext db, TimeProvider clock, ILogger<EfAgentTokens> logger) : IAgentTokens
{
    public async Task<AgentTokenPair> IssueAsync(CodeGrant grant)
    {
        var pair = AddPair(Guid.NewGuid(), grant.ClientId, grant.PresenterId, grant.Scope, grant.Resource, clock.GetUtcNow(), null);
        await db.SaveChangesAsync();
        return pair;
    }

    // The caller's write transaction (TokenGrants) holds SQLite's write lock from the code's spend to here, so an admin's
    // revoke lands either before this check or after these tokens exist, when RevokeAllForPresenterAsync ends them.
    public async Task<AgentTokenPair?> IssueForCodeAsync(string code, CodeGrant grant)
    {
        if (!await db.Users.AnyAsync(u => u.Id == grant.PresenterId && u.Access == UserAccess.Approved))
        {
            return null;
        }
        var pair = AddPair(FamilyOf(code), grant.ClientId, grant.PresenterId, grant.Scope, grant.Resource, clock.GetUtcNow(), null);
        await db.SaveChangesAsync();
        return pair;
    }

    public Task RevokeIssuedForCodeAsync(string code) => RevokeFamilyAsync(FamilyOf(code), clock.GetUtcNow());

    // The use mark only lands while the token is a live, unused refresh token of this client whose presenter is Approved,
    // all in one statement, so a revoke or a rejection that races the refresh can't leave a new pair behind. When the mark
    // misses, the row says why. Only a replay revokes, and only for an approved presenter and the right client: a refusal
    // for access changes nothing (S4), so replay detection is off while the presenter is not approved, and every token of
    // theirs is refused until they are approved again anyway.
    public async Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId)
    {
        var now = clock.GetUtcNow();
        var hash = SecretTokens.Hash(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marked = await db.AgentTokens
            .Where(t => t.TokenHash == hash
                && t.Kind == AgentTokenKind.Refresh
                && t.ClientId == clientId
                && t.RevokedAt == null
                && t.UsedAt == null
                && t.ExpiresAt > now
                && db.Users.Any(u => u.Id == t.PresenterId && u.Access == UserAccess.Approved))
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, now));
        if (marked == 1)
        {
            var row = await db.AgentTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash);
            // The family keeps its first IssuedAt (null stays null: it predates the connect time) and its last use.
            var pair = AddPair(row.FamilyId, row.ClientId, row.PresenterId, row.Scope, row.Resource, row.IssuedAt, row.LastUsedAt);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return pair;
        }
        var replayed = await db.AgentTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash
                && t.Kind == AgentTokenKind.Refresh
                && t.ClientId == clientId
                && t.RevokedAt == null
                && t.UsedAt != null
                && t.ExpiresAt > now
                && db.Users.Any(u => u.Id == t.PresenterId && u.Access == UserAccess.Approved))
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync();
        if (replayed is { } familyId)
        {
            await RevokeFamilyAsync(familyId, now);
        }
        await transaction.CommitAsync();
        return null;
    }

    // One read joins the token, its client and its presenter, so a deleted client or a presenter who is no longer approved
    // ends the token on its next use. The same read says whether the family's last use is stale, so a request writes at
    // most once per LastUsedInterval per family (D18).
    public async Task<AgentPrincipal?> ValidateAsync(string accessToken, string resource)
    {
        var now = clock.GetUtcNow();
        var hash = SecretTokens.Hash(accessToken);
        var found = await (
                from token in db.AgentTokens.AsNoTracking()
                join client in db.OAuthClients.AsNoTracking() on token.ClientId equals client.ClientId
                join presenter in db.Users.AsNoTracking() on token.PresenterId equals presenter.Id
                where token.TokenHash == hash
                    && token.Kind == AgentTokenKind.Access
                    && token.RevokedAt == null
                    && token.ExpiresAt > now
                    && token.Resource == resource
                    && presenter.Access == UserAccess.Approved
                select new { token.FamilyId, token.LastUsedAt, Principal = new AgentPrincipal(presenter.Id, presenter.Email, client.ClientName) })
            .SingleOrDefaultAsync();
        if (found is null)
        {
            return null;
        }
        if (found.LastUsedAt is null || found.LastUsedAt <= now - LastUsedInterval)
        {
            await StampLastUsedAsync(found.FamilyId, now);
        }
        return found.Principal;
    }

    public async Task RevokeAsync(string token)
    {
        var hash = SecretTokens.Hash(token);
        var familyId = await db.AgentTokens.AsNoTracking().Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync();
        if (familyId != null)
        {
            await RevokeFamilyAsync(familyId.Value, clock.GetUtcNow());
        }
    }

    public async Task RevokeAllForPresenterAsync(Guid presenterId)
    {
        var now = clock.GetUtcNow();
        await db.AgentTokens
            .Where(t => t.PresenterId == presenterId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now));
    }

    // The family of the tokens issued from a code: the first 128 bits of the code's stored hash, so nothing but
    // SecretTokens.Hash ever touches the code.
    private static Guid FamilyOf(string code) => Guid.ParseExact(SecretTokens.Hash(code)[..32], "N");

    private static readonly TimeSpan LastUsedInterval = TimeSpan.FromMinutes(5);

    // The use is a courtesy to the profile page: a failed write is logged and never fails the agent's request. The
    // condition re-checks the stamp, so parallel requests that all saw it stale write once.
    private async Task StampLastUsedAsync(Guid familyId, DateTimeOffset now)
    {
        var stale = now - LastUsedInterval;
        try
        {
            await db.AgentTokens
                .Where(t => t.FamilyId == familyId && (t.LastUsedAt == null || t.LastUsedAt <= stale))
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.LastUsedAt, now));
        }
        catch (DbException failure)
        {
            logger.LogWarning(failure, "Could not stamp the last use of token family {FamilyId}", familyId);
        }
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now)
        => db.AgentTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now));

    // Adds an access token (OAuthLifetimes.AccessToken) and a refresh token (OAuthLifetimes.RefreshToken) to the family;
    // the caller saves.
    private AgentTokenPair AddPair(Guid familyId, string clientId, Guid presenterId, string scope, string resource, DateTimeOffset? issuedAt, DateTimeOffset? lastUsedAt)
    {
        var now = clock.GetUtcNow();
        var access = Add(AgentTokenKind.Access, now + OAuthLifetimes.AccessToken);
        var refresh = Add(AgentTokenKind.Refresh, now + OAuthLifetimes.RefreshToken);
        return new AgentTokenPair(access, refresh, (int)OAuthLifetimes.AccessToken.TotalSeconds, scope);

        string Add(AgentTokenKind kind, DateTimeOffset expiresAt)
        {
            var token = SecretTokens.New();
            db.AgentTokens.Add(new AgentTokenRow
            {
                Id = Guid.NewGuid(),
                TokenHash = SecretTokens.Hash(token),
                Kind = kind,
                FamilyId = familyId,
                ClientId = clientId,
                PresenterId = presenterId,
                Scope = scope,
                Resource = resource,
                ExpiresAt = expiresAt,
                IssuedAt = issuedAt,
                LastUsedAt = lastUsedAt,
            });
            return token;
        }
    }
}
