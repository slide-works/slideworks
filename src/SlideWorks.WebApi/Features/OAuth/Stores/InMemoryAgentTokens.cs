using Agoda.IoC.Core;
using SlideWorks.WebApi.Data;

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

    // The checks, the use mark and the replay revocation happen under one lock, as one conditional update would in SQL.
    public Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (!_tokens.TryGetValue(OAuthSecrets.Hash(refreshToken), out var row)
                || row.Kind != AgentTokenKind.Refresh
                || row.RevokedAt != null
                || row.ClientId != clientId
                || row.ExpiresAt <= now)
            {
                return Task.FromResult<AgentTokenPair?>(null);
            }
            if (row.UsedAt != null)
            {
                RevokeFamily(row.FamilyId, now);
                return Task.FromResult<AgentTokenPair?>(null);
            }
            row.UsedAt = now;
            return Task.FromResult<AgentTokenPair?>(IssuePair(row.FamilyId, row.ClientId, row.PresenterId, row.Scope, row.Resource));
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
        return client == null || presenter == null ? null : new AgentPrincipal(presenter.Id, presenter.Email, client.ClientName);
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
