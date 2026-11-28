namespace SlideWorks.WebApi.Features.DeviceSessions.Views;

// POST /api/auth/device-sessions/exchange (K4). Nullable for the same reason as DeviceSignInCodeRequest: a missing field
// is the service's sign_in_expired or invalid_request, never the framework's 400.
public sealed record DeviceSessionExchangeRequest(string? Code, string? Verifier);
