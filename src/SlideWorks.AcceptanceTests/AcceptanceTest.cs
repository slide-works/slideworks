using Microsoft.Playwright.NUnit;

namespace SlideWorks.AcceptanceTests;

// Base for browser tests: a fresh browser context per test (so cookies, and with them the sign-in, do not leak between
// tests) pointed at the running BFF. A test signs in with SignIn.AsAsync(Page, TestUser.Sam), or stays anonymous. Tests
// reach the UI through page objects in Pages/, never through raw selectors.
public abstract class AcceptanceTest : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = AcceptanceApp.BaseUrl,
        ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
    };
}
