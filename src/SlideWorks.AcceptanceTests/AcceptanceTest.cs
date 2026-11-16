using Microsoft.Playwright.NUnit;
using SlideWorks.AcceptanceTests.Pages;

namespace SlideWorks.AcceptanceTests;

// Base for browser tests: a fresh browser context per test (so localStorage and the token do
// not leak between tests) pointed at the running BFF. Tests reach the UI through page
// objects in Pages/, never through raw selectors.
public abstract class AcceptanceTest : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = AcceptanceApp.BaseUrl,
        ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
    };

    protected async Task<NotesPage> OpenNotesPageAsync()
    {
        var login = new LoginPage(Page);
        await login.GoToAsync();
        return await login.SignInAsync(AcceptanceApp.TOKEN);
    }
}
