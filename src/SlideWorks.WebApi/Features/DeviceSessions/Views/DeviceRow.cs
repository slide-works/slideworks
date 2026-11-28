namespace SlideWorks.WebApi.Features.DeviceSessions.Views;

// One signed-in phone (K4), in device-sessions.json, device-session-exchange.json and the Profile's devices. The labels
// are formatted by the server in the person's time zone (IDisplayDates). Current is true only for the bearer caller's own
// session.
public sealed record DeviceRow(Guid Id, string DeviceName, string SignedInLabel, string LastUsedLabel, bool Current);
