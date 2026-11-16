using Agoda.IoC.Core;
using Microsoft.Playwright;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The one shared headless Chromium behind the card engine (D11). It starts on the first render, not with the app, and
// starts again when it has died (crashed or killed). It launches Playwright's default headless Chromium from
// PLAYWRIGHT_BROWSERS_PATH, with no channel and no executable path, so it runs whether the image installs full
// Chromium or only the headless shell. Disposed with the app.
[RegisterSingleton]
public sealed class RenderBrowser : IAsyncDisposable
{
    private readonly SemaphoreSlim _launch = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    // The running browser, launched now if there is none or the last one disconnected. Throws
    // RendererUnavailableException when Chromium or the Playwright driver can't start (not installed, or missing libraries).
    public async Task<IBrowser> GetAsync(CancellationToken ct)
    {
        await _launch.WaitAsync(ct);
        try
        {
            if (_browser is { IsConnected: true })
            {
                return _browser;
            }
            if (_browser != null)
            {
                await _browser.DisposeAsync();
                _browser = null;
            }
            try
            {
                _playwright ??= await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }
            catch (PlaywrightException e)
            {
                // The driver may be what died, so the next launch starts a new one.
                _playwright?.Dispose();
                _playwright = null;
                throw new RendererUnavailableException($"Chromium failed to start: {e.Message}");
            }
            return _browser;
        }
        finally
        {
            _launch.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.DisposeAsync();
        }
        _playwright?.Dispose();
        _launch.Dispose();
    }
}
