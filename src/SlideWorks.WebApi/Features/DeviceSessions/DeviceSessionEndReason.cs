namespace SlideWorks.WebApi.Features.DeviceSessions;

// Why a device session ended (K5). camelCase on the wire and in logs (appSignOut, webSignOut, expired, accessChanged);
// stored by name.
public enum DeviceSessionEndReason
{
    AppSignOut,
    WebSignOut,
    Expired,
    AccessChanged,
}
