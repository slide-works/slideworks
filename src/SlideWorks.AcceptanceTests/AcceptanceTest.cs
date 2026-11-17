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

    // Unloads the page's document before a test signs someone else in or out. A page left open keeps running under
    // the new cookie: when one of its requests then comes back 401 or pending_approval, the API client's handler
    // navigates to /signin or /pending, and that navigation supersedes the test's next GotoAsync (net::ERR_ABORTED, or
    // the test lands on /pending). Call it while the old session is still valid, so the page can't redirect on the
    // way out; once about:blank has loaded, nothing of the old page is left to answer.
    protected async Task LeavePageAsync() => await Page.GotoAsync("about:blank");
}
