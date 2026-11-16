using Agoda.IoC.Core;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.OAuth.Registration;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The browser leg of the authorization code flow. The client and its redirect URI (exact, or on loopback http any port,
// RFC 8252 7.3; the request keeps the URI as sent, so the token endpoint matches that one) are checked first: until both
// are known good, an error is a 400 page and never a redirect (RFC 6749 4.1.2.1). After that, a protocol error goes back
// to the client as error= on its redirect URI. Then a signed-out browser goes to sign in and comes back here with the
// same query, a person who isn't approved goes to /pending, and an approved one gets a request bound to them and the
// consent page.
[RegisterPerRequest(Concrete = true)]
public class AuthorizeHandler(IOAuthClients clients, IOAuthRequests requests, ICurrentActor actor, IPublicUrls urls)
{
    public async Task<IActionResult> HandleAsync(AuthorizeQuery query, string pathAndQuery)
    {
        var client = string.IsNullOrEmpty(query.ClientId) ? null : await clients.FindAsync(query.ClientId);
        if (client == null)
        {
            return ErrorPage("This agent isn't registered with SlideWorks (unknown client_id). Go back to the agent and connect again.");
        }
        if (query.RedirectUri == null || !RedirectUriRules.Matches(client.RedirectUris, query.RedirectUri))
        {
            return ErrorPage("The redirect_uri doesn't match one the agent registered, so SlideWorks won't send you back to it.");
        }
        var redirectUri = query.RedirectUri;
        if (Problem(query) is { } problem)
        {
            return new RedirectResult(OAuthRedirect.To(redirectUri, ("error", problem.Error), ("error_description", problem.Description), ("state", query.State)));
        }
        if (!actor.IsSignedIn)
        {
            return new RedirectResult("/signin?returnTo=" + Uri.EscapeDataString(pathAndQuery));
        }
        if (actor.Access != UserAccess.Approved)
        {
            return new RedirectResult("/pending");
        }
        var requestId = await requests.CreateAsync(new PendingAuthorization(
            client.ClientId, actor.PresenterId!.Value, redirectUri, query.State, query.CodeChallenge!, OAuthScopes.DECKS, urls.Mcp));
        return new RedirectResult("/oauth/consent?request=" + Uri.EscapeDataString(requestId));
    }

    private (string Error, string Description)? Problem(AuthorizeQuery query)
    {
        if (query.ResponseType != "code")
        {
            return ("unsupported_response_type", "Only response_type=code is supported.");
        }
        if (query.CodeChallengeMethod != "S256" || !IsS256Challenge(query.CodeChallenge))
        {
            return ("invalid_request", "PKCE is required: send code_challenge with code_challenge_method=S256.");
        }
        if (!string.IsNullOrEmpty(query.Scope) && query.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(scope => scope != OAuthScopes.DECKS))
        {
            return ("invalid_scope", $"The only scope is '{OAuthScopes.DECKS}'.");
        }
        // A client that leaves resource out gets the one resource there is; one that names another is refused.
        if (query.Resource != null && query.Resource != urls.Mcp)
        {
            return ("invalid_target", $"The only resource is {urls.Mcp}.");
        }
        return null;
    }

    // base64url of a SHA-256 hash: 43 characters, no padding.
    private static bool IsS256Challenge(string? challenge)
        => challenge is { Length: 43 } && challenge.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static ContentResult ErrorPage(string message)
        => new() { StatusCode = StatusCodes.Status400BadRequest, ContentType = "text/plain; charset=utf-8", Content = message };
}
