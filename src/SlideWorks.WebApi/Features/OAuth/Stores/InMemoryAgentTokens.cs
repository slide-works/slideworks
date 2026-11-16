using Agoda.IoC.Core;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// TODO: replace with the EF store on AgentTokenRow, rotating by conditional update; every agent has to connect again
// after a restart until then.
[RegisterSingleton(For = typeof(IAgentTokens))]
public class InMemoryAgentTokens(TimeProvider clock, IServiceScopeFactory scopes) : IAgentTokens
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AgentTokenRow> _tokens = new(StringComparer.Ordinal);

    public Task<AgentTokenPair> IssueAsync(CodeGrant grant)
    {
        lock (_gate)
        {
            return Task.FromResult(IssuePair(Guid.NewGuid(), grant.ClientId, grant.PresenterId, grant.Scope, grant.Resource));
        }
    }

    // The presenter's access is read before the lock, and a refusal changes nothing, so re-approval makes an unexpired
    // refresh token work again; only a revoke kills tokens. That gap is safe: a revoke calls RevokeAllForPresenterAsync
    // before it changes the state, and ValidateAsync refuses a non-approved presenter on every use. The checks, the use
    // mark and the replay revocation then happen under one lock, as one conditional update would in SQL.
    public async Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId)
    {
        Guid? presenterId;
        lock (_gate)
        {
            presenterId = _tokens.TryGetValue(OAuthSecrets.Hash(refreshToken), out var known) ? known.PresenterId : null;
        }
        if (presenterId != null && !await IsApprovedAsync(presenterId.Value))
        {
            return null;
        }
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (!_tokens.TryGetValue(OAuthSecrets.Hash(refreshToken), out var row)
                || row.Kind != AgentTokenKind.Refresh
                || row.RevokedAt != null
                || row.ClientId != clientId
                || row.ExpiresAt <= now)
            {
                return null;
            }
            if (row.UsedAt != null)
            {
                RevokeFamily(row.FamilyId, now);
                return null;
            }
            row.UsedAt = now;
            return IssuePair(row.FamilyId, row.ClientId, row.PresenterId, row.Scope, row.Resource);
        }
    }

    public async Task<AgentPrincipal?> ValidateAsync(string accessToken, string resource)
    {
        var now = clock.GetUtcNow();
        AgentTokenRow row;
        lock (_gate)
        {
            if (!_tokens.TryGetValue(OAuthSecrets.Hash(accessToken), out var found)
                || found.Kind != AgentTokenKind.Access
                || found.RevokedAt != null
                || found.ExpiresAt <= now
                || found.Resource != resource)
            {
                return null;
            }
            row = found;
        }
        // The client and presenter are read per call, like the real store's join, so a store swap changes nothing.
        using var scope = scopes.CreateScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOAuthClients>().FindAsync(row.ClientId);
        var presenter = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(row.PresenterId);
        return client == null || presenter is not { Access: UserAccess.Approved } ? null : new AgentPrincipal(presenter.Id, presenter.Email, client.ClientName);
    }

    public Task RevokeAsync(string token)
    {
        lock (_gate)
        {
            if (_tokens.TryGetValue(OAuthSecrets.Hash(token), out var row))
            {
                RevokeFamily(row.FamilyId, clock.GetUtcNow());
            }
        }
        return Task.CompletedTask;
    }

    public Task RevokeAllForPresenterAsync(Guid presenterId)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            foreach (var familyId in _tokens.Values.Where(t => t.PresenterId == presenterId).Select(t => t.FamilyId).Distinct().ToList())
            {
                RevokeFamily(familyId, now);
            }
        }
        return Task.CompletedTask;
    }

    private async Task<bool> IsApprovedAsync(Guid presenterId)
    {
        using var scope = scopes.CreateScope();
        var presenter = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(presenterId);
        return presenter is { Access: UserAccess.Approved };
    }

    // Callers hold _gate.
    private AgentTokenPair IssuePair(Guid familyId, string clientId, Guid presenterId, string scope, string resource)
    {
        var now = clock.GetUtcNow();
        var access = Add(AgentTokenKind.Access, now + OAuthLifetimes.AccessToken);
        var refresh = Add(AgentTokenKind.Refresh, now + OAuthLifetimes.RefreshToken);
        return new AgentTokenPair(access, refresh, (int)OAuthLifetimes.AccessToken.TotalSeconds, scope);

        string Add(AgentTokenKind kind, DateTimeOffset expiresAt)
        {
            var token = OAuthSecrets.New();
            var row = new AgentTokenRow
            {
                Id = Guid.NewGuid(),
                TokenHash = OAuthSecrets.Hash(token),
                Kind = kind,
                FamilyId = familyId,
                ClientId = clientId,
                PresenterId = presenterId,
                Scope = scope,
                Resource = resource,
                ExpiresAt = expiresAt,
            };
            _tokens[row.TokenHash] = row;
            return token;
        }
    }

    // Callers hold _gate.
    private void RevokeFamily(Guid familyId, DateTimeOffset now)
    {
        foreach (var member in _tokens.Values.Where(t => t.FamilyId == familyId && t.RevokedAt == null))
        {
            member.RevokedAt = now;
        }
    }
}
