using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Agoda.IoC.Core;
using Microsoft.Extensions.Configuration;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.DeviceSessions.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.DeviceSessions;

// A phone's sign-in and its signed-in devices (spec §3, K4): the confirm page mints a code from the cookie session, the app
// exchanges it with its PKCE verifier for a device session, and the person lists and revokes their devices. A caller signed
// in by a device session (the slideworks:device_session claim) can't mint, and is the one row marked current.
[RegisterPerRequest]
public sealed partial class DeviceSignIn(
    IDeviceSessions sessions,
    IUserAccounts accounts,
    ICurrentActor actor,
    MeViews me,
    IPublicUrls urls,
    ExchangeLimiter limiter,
    IConfiguration configuration)
{
    public const string IDENTITY_PROXY_SETTING = "SLIDEWORKS_IDENTITY_PROXY";

    public const string REDIRECT = "slideworks://signed-in";

    public const int DEVICE_NAME_MAX = 80;

    public async Task<DeviceSignInCodeView> MintAsync(DeviceSignInCodeRequest request, ClaimsPrincipal caller)
    {
        if (DeviceSessionOf(caller) != null)
        {
            throw new RefusalException(StatusCodes.Status403Forbidden, "cookie_required", "Sign in on the web to approve a phone.");
        }
        // Read on every call, so the setting is honoured as the server is configured now.
        if (configuration.GetValue<bool>(IDENTITY_PROXY_SETTING))
        {
            throw new RefusalException(
                StatusCodes.Status400BadRequest,
                "identity_proxy",
                "The SlideWorks app can't sign in to this server, because it sits behind an identity proxy.");
        }
        var challenge = Required(request.Challenge, ChallengePattern(), "challenge must be 43 base64url characters.");
        var state = Required(request.State, StatePattern(), "state must be 16 to 128 base64url characters.");
        var deviceName = request.DeviceName?.Trim();
        if (string.IsNullOrEmpty(deviceName) || deviceName.Length > DEVICE_NAME_MAX)
        {
            throw InvalidRequest($"deviceName must be 1 to {DEVICE_NAME_MAX} characters.");
        }
        var user = await accounts.FindAsync(actor.PresenterId!.Value)
            ?? throw new NotFoundException("The signed-in person no longer exists.");
        // The stamp exactly as the database holds it: a fresher in-memory value has finer precision than SQLite keeps, and
        // every exchange would then find the person's access changed.
        var code = await sessions.MintCodeAsync(user.Id, deviceName, challenge, user.AccessChangedAt);
        return new DeviceSignInCodeView($"{REDIRECT}?code={code}&state={state}&iss={Uri.EscapeDataString(urls.Base)}");
    }

    public async Task<DeviceSessionExchangeView> ExchangeAsync(DeviceSessionExchangeRequest request, IPAddress? remoteAddress)
    {
        if (!limiter.TryAcquire(remoteAddress, out var retryAfter))
        {
            throw new TooManyRequestsException("too_many_attempts", retryAfter);
        }
        var redeemed = request.Code == null ? null : await sessions.RedeemCodeAsync(request.Code, request.Verifier ?? "");
        if (redeemed is not { } signedIn)
        {
            throw new RefusalException(StatusCodes.Status400BadRequest, "sign_in_expired", "Sign-in expired. Try again.");
        }
        var (session, secret) = await sessions.CreateAsync(signedIn.UserId, signedIn.DeviceName);
        var rows = await sessions.ListRowsAsync(signedIn.UserId, session.Id);
        return new DeviceSessionExchangeView(rows.Single(r => r.Id == session.Id), secret, await me.ForAsync(signedIn.UserId));
    }

    public async Task<DeviceSessionsView> ListAsync(ClaimsPrincipal caller)
        => new(await sessions.ListRowsAsync(actor.PresenterId!.Value, DeviceSessionOf(caller)));

    // Another person's id answers exactly as an unknown or ended one: EndAsync takes no person, so ownership is checked here.
    public async Task RevokeAsync(Guid id, ClaimsPrincipal caller)
    {
        var rows = await sessions.ListRowsAsync(actor.PresenterId!.Value);
        if (!rows.Any(r => r.Id == id))
        {
            throw new NotFoundException("Not found");
        }
        await sessions.EndAsync(id, DeviceSessionOf(caller) == null ? DeviceSessionEndReason.WebSignOut : DeviceSessionEndReason.AppSignOut);
    }

    // The device session a bearer caller is signed in with; null for a cookie caller.
    private static Guid? DeviceSessionOf(ClaimsPrincipal caller)
        => Guid.TryParse(caller.FindFirst(SlideWorksClaims.DEVICE_SESSION_ID)?.Value, out var id) ? id : null;

    private static string Required(string? value, Regex pattern, string message)
        => value != null && pattern.IsMatch(value) ? value : throw InvalidRequest(message);

    private static RefusalException InvalidRequest(string message)
        => new(StatusCodes.Status400BadRequest, "invalid_request", message);

    [GeneratedRegex(@"^[A-Za-z0-9_-]{43}\z")]
    private static partial Regex ChallengePattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{16,128}\z")]
    private static partial Regex StatePattern();
}
