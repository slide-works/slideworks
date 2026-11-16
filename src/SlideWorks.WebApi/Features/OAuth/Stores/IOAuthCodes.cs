namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Authorization codes. Stored by hash, single use, alive for OAuthLifetimes.Code, and bound to the client, redirect URI,
// PKCE challenge (S256) and resource they were issued for.
public interface IOAuthCodes
{
    // Returns the code to put on the redirect.
    Task<string> IssueAsync(string clientId, Guid presenterId, string redirectUri, string codeChallenge, string scope, string resource);

    // Atomic. Any redeem of a live code spends it, even one that then fails a check, so a second attempt always gets null.
    // Null when the code is unknown, expired or spent, or the client, redirect URI, verifier or resource doesn't match.
    Task<CodeGrant?> RedeemAsync(string code, string clientId, string redirectUri, string codeVerifier, string resource);
}
