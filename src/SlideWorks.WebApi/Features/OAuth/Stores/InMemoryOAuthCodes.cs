using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// TODO: replace with the EF store on OAuthCodeRow, redeeming by conditional update; codes are lost on restart until then.
[RegisterSingleton(For = typeof(IOAuthCodes))]
public class InMemoryOAuthCodes(TimeProvider clock) : IOAuthCodes
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, OAuthCodeRow> _codes = new(StringComparer.Ordinal);

    public Task<string> IssueAsync(string clientId, Guid presenterId, string redirectUri, string codeChallenge, string scope, string resource)
    {
        var code = OAuthSecrets.New();
        var row = new OAuthCodeRow
        {
            CodeHash = OAuthSecrets.Hash(code),
            ClientId = clientId,
            PresenterId = presenterId,
            RedirectUri = redirectUri,
            CodeChallenge = codeChallenge,
            Scope = scope,
            Resource = resource,
            ExpiresAt = clock.GetUtcNow() + OAuthLifetimes.Code,
        };
        lock (_gate)
        {
            _codes[row.CodeHash] = row;
        }
        return Task.FromResult(code);
    }

    // Spending the code and checking it happen under one lock, as one conditional update would in SQL. The bindings are
    // checked after the spend, so a failed attempt still uses the code up.
    public Task<CodeGrant?> RedeemAsync(string code, string clientId, string redirectUri, string codeVerifier, string resource)
    {
        var now = clock.GetUtcNow();
        OAuthCodeRow row;
        lock (_gate)
        {
            if (!_codes.TryGetValue(OAuthSecrets.Hash(code), out var found) || found.RedeemedAt != null || found.ExpiresAt <= now)
            {
                return Task.FromResult<CodeGrant?>(null);
            }
            found.RedeemedAt = now;
            row = found;
        }
        var matches = row.ClientId == clientId
            && row.RedirectUri == redirectUri
            && row.Resource == resource
            && OAuthSecrets.VerifierMatches(codeVerifier, row.CodeChallenge);
        return Task.FromResult(matches ? new CodeGrant(row.ClientId, row.PresenterId, row.Scope, row.Resource) : null);
    }
}
