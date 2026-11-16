using System.Security.Claims;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// Registered as itself: the handler resolves EventsType from the request services.
// The handler has already checked state, the correlation cookie, PKCE, nonce, signature, issuer, audience and expiry.
// Here Google's verified email becomes a person (IUserDirectory, keyed by Google subject) and the ordinary session
// cookie; Google's own ticket is never signed in. Every failure lands on /signin?error=<SignInErrors code>. Logs name
// the kind of failure, never a token or an address.
[RegisterPerRequest(Concrete = true)]
public sealed class GoogleSignInEvents(IUserDirectory users, ILogger<GoogleSignInEvents> logger) : OpenIdConnectEvents
{
    private const string SUBJECT_CLAIM = "sub";
    private const string EMAIL_CLAIM = "email";
    private const string EMAIL_VERIFIED_CLAIM = "email_verified";
    private const string NAME_CLAIM = "name";
    private const string PICTURE_CLAIM = "picture";

    // Google answers with a top-level GET (query response mode). The handler would also read a form POST to the
    // callback; nothing legitimate sends one, so it is refused rather than left as a write outside /api.
    public override Task MessageReceived(MessageReceivedContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Fail("The sign-in callback only accepts GET.");
        }
        return Task.CompletedTask;
    }

    public override async Task TicketReceived(TicketReceivedContext context)
    {
        context.HandleResponse();
        var returnTo = ReturnTo.LocalOrHome(context.ReturnUri);
        var claims = context.Principal!;
        var subject = claims.FindFirstValue(SUBJECT_CLAIM);
        var email = claims.FindFirstValue(EMAIL_CLAIM)?.Trim();
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Google sign-in refused: the ID token has no subject or no email");
            context.Response.Redirect(SignInErrors.SignInPath(SignInErrors.FAILED, returnTo));
            return;
        }
        if (!string.Equals(claims.FindFirstValue(EMAIL_VERIFIED_CLAIM), "true", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Google sign-in refused: the email is not verified");
            context.Response.Redirect(SignInErrors.SignInPath(SignInErrors.UNVERIFIED, returnTo));
            return;
        }
        var owner = await users.FindByEmailAsync(email);
        if (owner != null && owner.GoogleSubject != subject)
        {
            logger.LogWarning("Google sign-in refused: the email belongs to a person with another Google subject");
            context.Response.Redirect(SignInErrors.SignInPath(SignInErrors.EMAIL_LINKED, returnTo));
            return;
        }
        var user = await users.UpsertAsync(subject, email, claims.FindFirstValue(NAME_CLAIM) ?? email, claims.FindFirstValue(PICTURE_CLAIM));
        // A pending person gets a session, and the app sends them to wait for approval. A rejected one gets none.
        if (user.Access == UserAccess.Rejected)
        {
            logger.LogInformation("Google sign-in refused: the account is rejected");
            context.Response.Redirect(SignInErrors.SignInPath(SignInErrors.NOT_APPROVED, returnTo));
            return;
        }
        await context.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            SessionPrincipal.For(user, CookieAuthenticationDefaults.AuthenticationScheme),
            new AuthenticationProperties { IsPersistent = true });
        context.Response.Redirect(returnTo);
    }

    public override Task AccessDenied(AccessDeniedContext context)
    {
        logger.LogInformation("Google sign-in cancelled on Google's consent screen");
        context.HandleResponse();
        context.Response.Redirect(SignInErrors.SignInPath(SignInErrors.CANCELLED, ReturnTo.LocalOrHome(context.ReturnUrl)));
        return Task.CompletedTask;
    }

    // A stale or forged state, a missing correlation cookie, a failed code exchange or token check, or Google unreachable.
    public override Task RemoteFailure(RemoteFailureContext context)
    {
        logger.LogWarning("Google sign-in failed ({ErrorType})", context.Failure?.GetType().Name ?? "unknown");
        context.HandleResponse();
        context.Response.Redirect(SignInErrors.SignInPath(SignInErrors.FAILED, ReturnTo.LocalOrHome(context.Properties?.RedirectUri)));
        return Task.CompletedTask;
    }
}
