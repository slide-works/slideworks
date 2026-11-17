namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Access and refresh tokens, stored by hash. The tokens issued from one code form a family, and every refresh stays in it.
public interface IAgentTokens
{
    // Starts a family: an access token (OAuthLifetimes.AccessToken) and a refresh token (OAuthLifetimes.RefreshToken).
    Task<AgentTokenPair> IssueAsync(CodeGrant grant);

    // IssueAsync for the code that was just redeemed into this grant: the family is tied to the code, so presenting the
    // code again can end it (RevokeIssuedForCodeAsync). Null, with nothing issued, when the grant's presenter is not
    // Approved, as RefreshAsync and ValidateAsync refuse them: a code issued just before an admin revoked its presenter
    // yields no tokens that re-approval could bring back.
    Task<AgentTokenPair?> IssueForCodeAsync(string code, CodeGrant grant);

    // RFC 6749 section 4.1.2: a code presented again after it was redeemed revokes the family issued from it. A code that
    // never got tokens (unknown, unredeemed or refused) changes nothing.
    Task RevokeIssuedForCodeAsync(string code);

    // Atomic. Marks the refresh token used and issues a new pair in its family. Null when the token is unknown, expired,
    // revoked, not a refresh token or for another client, and when the presenter is not Approved. That last check comes
    // first and changes nothing: the token is not used and the family is not revoked, so re-approval makes an unexpired
    // refresh token work again. A refresh token that was already used is a replay: the whole family is revoked,
    // including the pair the first use got, and the result is null.
    Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId);

    // Null unless the access token is known, live, unrevoked and issued for this resource, its client exists and its
    // presenter exists and is Approved.
    Task<AgentPrincipal?> ValidateAsync(string accessToken, string resource);

    // Revokes the family of the access or refresh token. An unknown token is ignored.
    Task RevokeAsync(string token);

    // Revokes every family of this presenter. Nobody to revoke is not an error. Only a revoke kills tokens for good: a
    // presenter who is not approved is refused without it, and approving them again revives what was not revoked.
    Task RevokeAllForPresenterAsync(Guid presenterId);
}
