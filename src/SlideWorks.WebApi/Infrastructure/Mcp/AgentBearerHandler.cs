using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Infrastructure.Mcp;

// The AgentBearer scheme: an OAuth access token in the Authorization header, issued for the resource {public}/mcp. The
// token is checked on every request and never logged. A missing or bad token gets 401 with the resource metadata URL,
// so a client can discover the authorization server and connect with only the /mcp URL.
public class AgentBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAgentTokens tokens,
    IPublicUrls urls)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BEARER_PREFIX = "Bearer ";

    private bool _tokenRefused;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BEARER_PREFIX, StringComparison.OrdinalIgnoreCase) || header[BEARER_PREFIX.Length..].Trim() is not { Length: > 0 } token)
        {
            return AuthenticateResult.NoResult();
        }
        var agent = await tokens.ValidateAsync(token, urls.Mcp);
        if (agent == null)
        {
            _tokenRefused = true;
            return AuthenticateResult.Fail("The access token is unknown, expired, revoked or for another resource.");
        }
        var identity = new ClaimsIdentity(
            [
                new Claim(SlideWorksClaims.PRESENTER_ID, agent.PresenterId.ToString()),
                new Claim(SlideWorksClaims.EMAIL, agent.Email),
                new Claim(SlideWorksClaims.NAME, agent.ClientName),
                new Claim(SlideWorksClaims.AGENT_CLIENT_NAME, agent.ClientName),
                new Claim(SlideWorksClaims.ACTOR_KIND, nameof(ActorKind.Agent)),
            ],
            Scheme.Name,
            SlideWorksClaims.NAME,
            null);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    // Never a redirect: 401 with the challenge RFC 9728 describes, plus error="invalid_token" when a token was refused.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var challenge = $"Bearer resource_metadata=\"{urls.Base}/.well-known/oauth-protected-resource\"";
        if (_tokenRefused)
        {
            challenge += ", error=\"invalid_token\"";
        }
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append(HeaderNames.WWWAuthenticate, challenge);
        return Task.CompletedTask;
    }
}
