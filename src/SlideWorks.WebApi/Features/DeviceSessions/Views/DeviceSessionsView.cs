namespace SlideWorks.WebApi.Features.DeviceSessions.Views;

// GET /api/auth/device-sessions (device-sessions.json, K4): the person's live sessions, newest first.
public sealed record DeviceSessionsView(IReadOnlyList<DeviceRow> Devices);
