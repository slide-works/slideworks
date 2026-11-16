using Agoda.IoC.Core;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// The token endpoint's grants and the revoke endpoint. Clients are public (no secret), so client_id names the client and
// the code's PKCE verifier or the refresh token proves it. Tokens never reach a log or an error description.
[RegisterPerRequest]
public class TokenGrants(AppDbContext db, IOAuthClients clients, IOAuthCodes codes, IAgentTokens tokens, IPublicUrls urls)
{
    public const string AUTHORIZATION_CODE = "authorization_code";
    public const string REFRESH_TOKEN = "refresh_token";

    private const string BEARER = "Bearer";

    public Task<TokenResponseView> ExchangeAsync(TokenRequest request) => request.GrantType switch
    {
        AUTHORIZATION_CODE => RedeemCodeAsync(request),
        REFRESH_TOKEN => RefreshAsync(request),
        null or "" => throw Invalid("invalid_request", "grant_type is required."),
        _ => throw Invalid("unsupported_grant_type", $"Only {AUTHORIZATION_CODE} and {REFRESH_TOKEN} are supported."),
    };

    public Task RevokeAsync(RevokeRequest request)
        => string.IsNullOrEmpty(request.Token) ? throw Invalid("invalid_request", "token is required.") : tokens.RevokeAsync(request.Token);

    // The code is spent by any redeem that gets that far, even one that fails a binding. Presenting a code that was
    // already redeemed revokes the tokens issued from it (RFC 6749 section 4.1.2). The spend and the issue share one write
    // transaction (SQLite's BEGIN IMMEDIATE), so a replay that finds the code spent waits until the tokens it was spent
    // for exist, and its revoke always ends them.
    private async Task<TokenResponseView> RedeemCodeAsync(TokenRequest request)
    {
        var code = Required(request.Code, "code");
        var redirectUri = Required(request.RedirectUri, "redirect_uri");
        var verifier = Required(request.CodeVerifier, "code_verifier");
        var clientId = await KnownClientAsync(request.ClientId);
        RequireOurResource(request);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var grant = await codes.RedeemAsync(code, clientId, redirectUri, verifier, urls.Mcp);
        if (grant == null)
        {
            await tokens.RevokeIssuedForCodeAsync(code);
            await transaction.CommitAsync();
            throw Invalid("invalid_grant", "The code is invalid, expired or already used, or doesn't match this client, redirect_uri or code_verifier.");
        }
        var pair = await tokens.IssueForCodeAsync(code, grant);
        await transaction.CommitAsync();
        return View(pair);
    }

    private async Task<TokenResponseView> RefreshAsync(TokenRequest request)
    {
        var refreshToken = Required(request.RefreshToken, "refresh_token");
        var clientId = await KnownClientAsync(request.ClientId);
        RequireOurResource(request);
        var pair = await tokens.RefreshAsync(refreshToken, clientId)
            ?? throw Invalid("invalid_grant", "The refresh token is invalid, expired, revoked or for another client.");
        return View(pair);
    }

    private async Task<string> KnownClientAsync(string? clientId)
    {
        var id = Required(clientId, "client_id");
        return await clients.FindAsync(id) == null
            ? throw new OAuthTokenException(StatusCodes.Status401Unauthorized, "invalid_client", "Unknown client_id. Register the client again.")
            : id;
    }

    // RFC 8707: a client that leaves resource out gets the one resource there is; one that names another is refused.
    private void RequireOurResource(TokenRequest request)
    {
        if (request.Resource != null && request.Resource != urls.Mcp)
        {
            throw Invalid("invalid_target", $"The only resource is {urls.Mcp}.");
        }
    }

    private static string Required(string? value, string name)
        => string.IsNullOrEmpty(value) ? throw Invalid("invalid_request", $"{name} is required.") : value;

    private static TokenResponseView View(AgentTokenPair pair)
        => new(pair.AccessToken, BEARER, pair.ExpiresInSeconds, pair.RefreshToken, pair.Scope);

    private static OAuthTokenException Invalid(string error, string description)
        => new(StatusCodes.Status400BadRequest, error, description);
}
