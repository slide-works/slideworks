namespace SlideWorks.WebApi.Features.DeviceSessions.Data;

// A sign-in code the confirm page mints for the app (K4), stored by its SHA-256 hex with the device name, the PKCE
// challenge and the person's User.AccessChangedAt at mint time. Lives two minutes; single use, marked used on the first
// redeem whatever its outcome.
public class DeviceSignInCode
{
    public required string CodeHash { get; set; }

    public Guid UserId { get; set; }

    public required string DeviceName { get; set; }

    public required string Challenge { get; set; }

    public DateTimeOffset? AccessStamp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
