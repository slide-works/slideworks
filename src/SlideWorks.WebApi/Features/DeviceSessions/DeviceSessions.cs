using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.DeviceSessions.Data;
using SlideWorks.WebApi.Features.DeviceSessions.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.DeviceSessions;

// The device-session store (spec §3, K4, K5): the sessions a phone signs in with and the codes the confirm page mints for
// it. Secrets and codes are kept only as SHA-256 hex and never logged. Times come from TimeProvider.
public interface IDeviceSessions
{
    // A new live session for the person, and its secret, which is returned this once and stored only as its hash.
    Task<(DeviceSession Session, string Secret)> CreateAsync(Guid userId, string deviceName);

    // The session the secret names, live or not, or null when it names none.
    Task<DeviceSession?> FindBySecretAsync(string secret);

    // Marks a live session used, at most once a minute, sliding its expiry to 90 days from now. Never revives an ended or
    // expired session.
    Task TouchAsync(DeviceSession session);

    bool IsLive(DeviceSession session, DateTimeOffset now);

    // Ends the session with the reason. Idempotent: true only for the call that ended it, and only that call tells every
    // IDeviceSessionEndedObserver, after the end is committed.
    Task<bool> EndAsync(Guid sessionId, DeviceSessionEndReason reason);

    // The person's live sessions as K4 rows, newest first; Current marks currentSessionId's row.
    Task<IReadOnlyList<DeviceRow>> ListRowsAsync(Guid userId, Guid? currentSessionId = null);

    // A new single-use code for the person's phone, returned this once and stored only as its hash with the device name,
    // the PKCE challenge and accessStamp (the person's User.AccessChangedAt).
    Task<string> MintCodeAsync(Guid userId, string deviceName, string challenge, DateTimeOffset? accessStamp);

    // The person and device name a code was minted for, or null for an unknown, used or expired code, a verifier that
    // doesn't match its challenge, or a person who is missing or whose access changed since minting. A known code is
    // marked used whatever the outcome.
    Task<(Guid UserId, string DeviceName)?> RedeemCodeAsync(string code, string verifier);
}

[RegisterPerRequest]
public sealed partial class DeviceSessions(
    AppDbContext db,
    TimeProvider clock,
    IDisplayDates dates,
    IEnumerable<IDeviceSessionEndedObserver> observers,
    ILogger<DeviceSessions> logger) : IDeviceSessions
{
    public static readonly TimeSpan LIFETIME = TimeSpan.FromDays(90);

    public static readonly TimeSpan TOUCH_INTERVAL = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan CODE_LIFETIME = TimeSpan.FromMinutes(2);

    public async Task<(DeviceSession Session, string Secret)> CreateAsync(Guid userId, string deviceName)
    {
        var now = clock.GetUtcNow();
        var secret = DeviceSecrets.NewSecret();
        var session = new DeviceSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceName = deviceName,
            SecretHash = DeviceSecrets.Hash(secret),
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now + LIFETIME,
        };
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.DeviceSessions.Add(session);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        LogCreated(session.Id, userId);
        return (session, secret);
    }

    public async Task<DeviceSession?> FindBySecretAsync(string secret)
    {
        var hash = DeviceSecrets.Hash(secret);
        return await db.DeviceSessions.AsNoTracking().SingleOrDefaultAsync(s => s.SecretHash == hash);
    }

    public async Task TouchAsync(DeviceSession session)
    {
        var now = clock.GetUtcNow();
        if (now - session.LastUsedAt < TOUCH_INTERVAL || !IsLive(session, now))
        {
            return;
        }
        var touchedBefore = now - TOUCH_INTERVAL;
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.DeviceSessions
            .Where(s => s.Id == session.Id && s.EndedAt == null && s.ExpiresAt > now && s.LastUsedAt <= touchedBefore)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastUsedAt, now).SetProperty(s => s.ExpiresAt, now + LIFETIME));
        await transaction.CommitAsync();
    }

    public bool IsLive(DeviceSession session, DateTimeOffset now) => session.EndedAt == null && session.ExpiresAt > now;

    public async Task<bool> EndAsync(Guid sessionId, DeviceSessionEndReason reason)
    {
        var now = clock.GetUtcNow();
        int ended;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            ended = await db.DeviceSessions
                .Where(s => s.Id == sessionId && s.EndedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.EndedAt, now).SetProperty(s => s.EndReason, (DeviceSessionEndReason?)reason));
            await transaction.CommitAsync();
        }
        if (ended == 0)
        {
            return false;
        }
        LogEnded(sessionId, JsonNamingPolicy.CamelCase.ConvertName(reason.ToString()));
        foreach (var observer in observers)
        {
            await observer.OnEndedAsync(sessionId);
        }
        return true;
    }

    public async Task<IReadOnlyList<DeviceRow>> ListRowsAsync(Guid userId, Guid? currentSessionId = null)
    {
        var now = clock.GetUtcNow();
        var sessions = await db.DeviceSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.EndedAt == null && s.ExpiresAt > now)
            .ToListAsync();
        return
        [
            .. sessions
                .OrderByDescending(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .Select(s => new DeviceRow(s.Id, s.DeviceName, dates.Ago(s.CreatedAt), dates.Ago(s.LastUsedAt), s.Id == currentSessionId)),
        ];
    }

    public async Task<string> MintCodeAsync(Guid userId, string deviceName, string challenge, DateTimeOffset? accessStamp)
    {
        var code = DeviceSecrets.NewCode();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.DeviceSignInCodes.Add(new DeviceSignInCode
        {
            CodeHash = DeviceSecrets.Hash(code),
            UserId = userId,
            DeviceName = deviceName,
            Challenge = challenge,
            AccessStamp = accessStamp,
            CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return code;
    }

    public async Task<(Guid UserId, string DeviceName)?> RedeemCodeAsync(string code, string verifier)
    {
        var now = clock.GetUtcNow();
        var hash = DeviceSecrets.Hash(code);
        DeviceSignInCode? row;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // Marking the code used is the first write, so a second redeem of the same code, even a racing one, finds it used.
            var marked = await db.DeviceSignInCodes
                .Where(c => c.CodeHash == hash && c.UsedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.UsedAt, now));
            row = marked == 0 ? null : await db.DeviceSignInCodes.AsNoTracking().SingleAsync(c => c.CodeHash == hash);
            await transaction.CommitAsync();
        }
        if (row == null || now - row.CreatedAt > CODE_LIFETIME || !DeviceSecrets.ChallengeMatches(verifier, row.Challenge))
        {
            return null;
        }
        var user = await db.Users.AsNoTracking().Where(u => u.Id == row.UserId).Select(u => new { u.AccessChangedAt }).SingleOrDefaultAsync();
        if (user == null || user.AccessChangedAt != row.AccessStamp)
        {
            return null;
        }
        return (row.UserId, row.DeviceName);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Device session {SessionId} created for {UserId}")]
    private partial void LogCreated(Guid sessionId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Device session {SessionId} ended: {Reason}")]
    private partial void LogEnded(Guid sessionId, string reason);
}
