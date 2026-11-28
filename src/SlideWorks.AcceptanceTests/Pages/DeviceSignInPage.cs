namespace SlideWorks.AcceptanceTests.Pages;

// /app/sign-in, the confirm page the SlideWorks app opens in the system browser to sign in (contract K6): which device
// and person, the warning, Continue and Cancel, then Open the app; or why not (an incomplete link, the server's
// refusal, a failure worth retrying, an unreachable server). Mirrors src/pages/deviceSignIn/testIds.ts.
public sealed class DeviceSignInPage(IPage page)
{
    public const string PATH = "/app/sign-in";

    public ILocator Root => page.GetByTestId("device-sign-in-page");

    public ILocator Heading => page.GetByTestId("device-sign-in-heading");

    public ILocator Email => page.GetByTestId("device-sign-in-email");

    public ILocator Warning => page.GetByTestId("device-sign-in-warning");

    public ILocator ContinueButton => page.GetByTestId("device-sign-in-continue");

    public ILocator CancelLink => page.GetByTestId("device-sign-in-cancel");

    public ILocator OpenAppLink => page.GetByTestId("device-sign-in-open-app");

    public ILocator IncompleteMessage => page.GetByTestId("device-sign-in-incomplete");

    public ILocator RefusedMessage => page.GetByTestId("device-sign-in-refused");

    public ILocator FailedMessage => page.GetByTestId("device-sign-in-failed");

    public ILocator Unreachable => page.GetByTestId("device-sign-in-unreachable");

    public Task GotoAsync(string challenge, string state, string device) =>
        page.GotoAsync(
            $"{PATH}?challenge={Uri.EscapeDataString(challenge)}&state={Uri.EscapeDataString(state)}&device={Uri.EscapeDataString(device)}");

    public Task ContinueAsync() => ContinueButton.ClickAsync();
}
