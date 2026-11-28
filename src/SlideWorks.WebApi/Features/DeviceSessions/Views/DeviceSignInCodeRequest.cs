namespace SlideWorks.WebApi.Features.DeviceSessions.Views;

// POST /api/auth/device-sessions/codes (K4). Every field is nullable so a missing one reaches the service, which refuses
// it with invalid_request naming the field, rather than the framework's own 400.
public sealed record DeviceSignInCodeRequest(string? Challenge, string? State, string? DeviceName);
