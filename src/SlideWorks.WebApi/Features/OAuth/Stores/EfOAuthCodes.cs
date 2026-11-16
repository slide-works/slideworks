using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Authorization codes, one OAuthCodeRow each, found by the code's hash. The code itself is never stored.
[RegisterPerRequest(For = typeof(IOAuthCodes))]
public class EfOAuthCodes(AppDbContext db, TimeProvider clock) : IOAuthCodes
{
    public async Task<string> IssueAsync(string clientId, Guid presenterId, string redirectUri, string codeChallenge, string scope, string resource)
    {
        var code = SecretTokens.New();
        db.OAuthCodes.Add(new OAuthCodeRow
        {
            CodeHash = SecretTokens.Hash(code),
            ClientId = clientId,
            PresenterId = presenterId,
            RedirectUri = redirectUri,
            CodeChallenge = codeChallenge,
            Scope = scope,
            Resource = resource,
            ExpiresAt = clock.GetUtcNow() + OAuthLifetimes.Code,
        });
        await db.SaveChangesAsync();
        return code;
    }

    // One conditional update spends the code, so of any number of parallel attempts exactly one gets past it. The bindings
    // are checked after the spend, so an attempt that fails them still uses the code up.
    public async Task<CodeGrant?> RedeemAsync(string code, string clientId, string redirectUri, string codeVerifier, string resource)
    {
        var now = clock.GetUtcNow();
        var hash = SecretTokens.Hash(code);
        var spent = await db.OAuthCodes
            .Where(c => c.CodeHash == hash && c.RedeemedAt == null && c.ExpiresAt > now)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.RedeemedAt, now));
        if (spent != 1)
        {
            return null;
        }
        var row = await db.OAuthCodes.AsNoTracking().SingleAsync(c => c.CodeHash == hash);
        var matches = row.ClientId == clientId
            && row.RedirectUri == redirectUri
            && row.Resource == resource
            && OAuthSecrets.VerifierMatches(codeVerifier, row.CodeChallenge);
        return matches ? new CodeGrant(row.ClientId, row.PresenterId, row.Scope, row.Resource) : null;
    }
}
