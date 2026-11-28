namespace SlideWorks.WebApi.Features.DeviceSessions.Data;

// A phone signed in to SlideWorks (spec §3, K5): a long-lived bearer secret, stored only as its SHA-256 hex. Live while
// EndedAt is null and ExpiresAt is in the future; an expired session is never revived. Written only by IDeviceSessions.
public class DeviceSession
{
    public const int DEVICE_NAME_MAX = 80;

    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string DeviceName { get; set; }

    public required string SecretHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastUsedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public DeviceSessionEndReason? EndReason { get; set; }
}
