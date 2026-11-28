namespace SlideWorks.WebApi.Features.DeviceSessions.Views;

// The minted code's hand-back to the app (device-session-code.json, K4):
// slideworks://signed-in?code=<code>&state=<state>&iss=<urlencoded public origin>.
public sealed record DeviceSignInCodeView(string OpenAppUrl);
