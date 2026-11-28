using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Features.DeviceSessions.Views;

// The exchange's 201 (device-session-exchange.json, K4): the new session's row, its secret (swd_ plus 43 base64url
// characters, shown this once) and the person as GET /api/me shows them.
public sealed record DeviceSessionExchangeView(DeviceRow Session, string Secret, MeView Me);
