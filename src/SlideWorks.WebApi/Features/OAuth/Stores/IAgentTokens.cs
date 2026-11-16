namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Access and refresh tokens, stored by hash. The tokens issued from one code form a family, and every refresh stays in it.
public interface IAgentTokens
{
    // Starts a family: an access token (OAuthLifetimes.AccessToken) and a refresh token (OAuthLifetimes.RefreshToken).
    Task<AgentTokenPair> IssueAsync(CodeGrant grant);

    // Atomic. Null when the presenter is not Approved, checked before anything else changes: a refusal for that reason does
    // not use the token or revoke the family, so re-approval makes an unexpired refresh token work again. Marks the refresh token used and issues a new pair in its family. Null when it is unknown, expired, revoked,
    // not a refresh token or for another client. A refresh token that was already used is a replay: the whole family is
    // revoked, including the pair the first use got, and the result is null.
    Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId);

    // Null unless the access token is known, live, unrevoked and issued for this resource, and its client and presenter
    // still exist, and unless the presenter's Access is Approved.
    Task<AgentPrincipal?> ValidateAsync(string accessToken, string resource);

    // Revokes the family of the access or refresh token. An unknown token is ignored.
    Task RevokeAsync(string token);

    // Revokes every family of this presenter. Nobody to revoke is not an error. Only a revoke kills tokens for good: a
    // presenter who is not approved is refused without it, and approving them again revives what was not revoked.
    Task RevokeAllForPresenterAsync(Guid presenterId);
}
