using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// TODO: replace with the EF store on OAuthRequestRow, consuming by conditional update; pending requests are lost on
// restart until then.
[RegisterSingleton(For = typeof(IOAuthRequests))]
public class InMemoryOAuthRequests(TimeProvider clock) : IOAuthRequests
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, OAuthRequestRow> _requests = new(StringComparer.Ordinal);

    public Task<string> CreateAsync(PendingAuthorization pending)
    {
        var id = OAuthSecrets.New();
        var row = new OAuthRequestRow
        {
            Id = id,
            ClientId = pending.ClientId,
            PresenterId = pending.PresenterId,
            RedirectUri = pending.RedirectUri,
            State = pending.State,
            CodeChallenge = pending.CodeChallenge,
            Scope = pending.Scope,
            Resource = pending.Resource,
            ExpiresAt = clock.GetUtcNow() + OAuthLifetimes.Request,
        };
        lock (_gate)
        {
            _requests[id] = row;
        }
        return Task.FromResult(id);
    }

    // The check and the consume happen under one lock, as one conditional update would in SQL.
    public Task<PendingAuthorization?> ConsumeAsync(string requestId, Guid presenterId)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (!_requests.TryGetValue(requestId, out var row) || row.ConsumedAt != null || row.ExpiresAt <= now || row.PresenterId != presenterId)
            {
                return Task.FromResult<PendingAuthorization?>(null);
            }
            row.ConsumedAt = now;
            return Task.FromResult<PendingAuthorization?>(
                new PendingAuthorization(row.ClientId, row.PresenterId, row.RedirectUri, row.State, row.CodeChallenge, row.Scope, row.Resource));
        }
    }
}
