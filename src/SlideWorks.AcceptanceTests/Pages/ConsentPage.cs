using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// /oauth/consent?request=<id>, where GET /oauth/authorize sends a signed-in, approved presenter: which agent asks, Allow
// and Deny, and the message for an expired, answered or someone else's request. Allow or Deny sends the browser on to
// the agent's redirect URI.
public sealed class ConsentPage(IPage page)
{
    public const string PATH = "/oauth/consent";

    public ILocator Root => page.GetByTestId("consent-page");

    public ILocator ClientName => page.GetByTestId("consent-client-name");

    public ILocator Scope => page.GetByTestId("consent-scope");

    // Where the answer goes: the redirect's host and port, or the app's own scheme.
    public ILocator ReturnsTo => page.GetByTestId("consent-returns-to-host");

    public ILocator Allow => page.GetByTestId("consent-allow");

    public ILocator Deny => page.GetByTestId("consent-deny");

    public ILocator Gone => page.GetByTestId("consent-gone");

    public async Task ShouldAskForAsync(string clientName)
    {
        await Expect(ClientName).ToHaveTextAsync(clientName);
        await Expect(Allow).ToBeVisibleAsync();
        await Expect(Deny).ToBeVisibleAsync();
    }

    public Task AllowAsync() => Allow.ClickAsync();

    public Task DenyAsync() => Deny.ClickAsync();
}
