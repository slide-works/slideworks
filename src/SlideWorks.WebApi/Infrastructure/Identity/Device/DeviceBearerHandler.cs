using System.Net.Mime;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Infrastructure.Setup;

namespace SlideWorks.WebApi.Infrastructure.Identity.Device;

// The DeviceBearer scheme (K5): the session a phone's swd_ secret names, checked on every request, never logged. A live
// session of a person who is not rejected signs the request in as that person, with the same claims as a cookie session
// plus the session's id, and is touched (at most once a minute, sliding its 90 days). An expired session is ended as
// expired the first time it is seen, and a session whose person is now missing or rejected is ended as accessChanged;
// either answers 401 session_ended, as does an ended one. A secret that names nothing answers 401 Not signed in.
//
// The store is read in a scope of its own: the request's per-request services include HttpCurrentActor, which takes the
// request's principal when it is first built, and authentication runs before there is one.
public sealed class DeviceBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopes,
    TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private string _refusal = AuthSetup.NOT_SIGNED_IN;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var secret = DeviceBearer.SecretOf(Request);
        if (secret == null)
        {
            return AuthenticateResult.NoResult();
        }
        await using var scope = scopes.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IDeviceSessions>();
        var session = await sessions.FindBySecretAsync(secret);
        if (session == null)
        {
            return AuthenticateResult.Fail("The device secret names no session.");
        }
        if (!sessions.IsLive(session, clock.GetUtcNow()))
        {
            if (session.EndedAt == null)
            {
                await sessions.EndAsync(session.Id, DeviceSessionEndReason.Expired);
            }
            return Ended();
        }
        var user = await scope.ServiceProvider.GetRequiredService<IUserAccounts>().FindAsync(session.UserId);
        if (user == null || user.Access == UserAccess.Rejected)
        {
            await sessions.EndAsync(session.Id, DeviceSessionEndReason.AccessChanged);
            return Ended();
        }
        await sessions.TouchAsync(session);
        var principal = SessionPrincipal.For(user, Scheme.Name);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(SlideWorksClaims.DEVICE_SESSION_ID, session.Id.ToString()));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    // Never a redirect, as the cookie scheme: 401 with session_ended or Not signed in.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) => WriteErrorAsync(StatusCodes.Status401Unauthorized, _refusal);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) => WriteErrorAsync(StatusCodes.Status403Forbidden, AuthSetup.NOT_ALLOWED);

    private AuthenticateResult Ended()
    {
        _refusal = DeviceBearer.SESSION_ENDED;
        return AuthenticateResult.Fail("The device session has ended.");
    }

    private Task WriteErrorAsync(int status, string error)
    {
        Response.StatusCode = status;
        Response.ContentType = MediaTypeNames.Application.Json;
        return Response.WriteAsJsonAsync(new ErrorResponse(error), Context.RequestAborted);
    }
}
