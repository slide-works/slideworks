using Agoda.IoC.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Text.Json;
using Microsoft.Playwright;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Features.Render;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The share card engine (L9, D11): the SPA's /card/{id} or /card/generic route in the shared headless Chromium, at
// 1200x630, screenshotted once the page says data-ready. At most two pages render at once. Each render gets a fresh
// browser context carrying a Card render token as the X-Render-Token header (and in the fragment, which the card page
// reads and which never reaches the server); every request to another origin is aborted. A data-error on the page or
// a failed image load fails the render with "Rendering failed: <text>", and a render that passes RenderOptions.Timeout
// fails with "Rendering timed out". When the browser dies mid-render, it is started again and the render retried once.
[RegisterSingleton(For = typeof(IRenderEngine))]
public sealed class PlaywrightRenderEngine(RenderBrowser browser, IRenderTokens tokens, IConfiguration configuration, IServer server) : IRenderEngine, IDisposable
{
    public const int WIDTH = 1200;
    public const int HEIGHT = 630;
    public const string TIMED_OUT = "Rendering timed out";
    public const string FAILED = "Rendering failed: ";

    private const int MAX_PAGES = 2;
    private const string MEASURE_TIMED_OUT = "Measuring the font timed out";
    private static readonly TimeSpan MEASURE_TIMEOUT = TimeSpan.FromSeconds(15);
    private const string CARD_TEST_ID = "share-card";
    private const string SETTLED = "() => { const card = document.querySelector('[data-testid=share-card]'); return !!card && (card.dataset.ready === 'true' || card.hasAttribute('data-error')); }";
    private const string IMAGE = "image";
    private const string PAGE_ERROR = "page error";

    private readonly SemaphoreSlim _pages = new(MAX_PAGES, MAX_PAGES);
    private readonly RenderOptions _options = RenderOptions.From(configuration);

    public Task<byte[]> RenderCardAsync(string deckId, CancellationToken ct) => RenderAsync(deckId, ct);

    public Task<byte[]> RenderGenericCardAsync(CancellationToken ct) => RenderAsync(null, ct);

    public const int SLIDE_WIDTH = 1920;
    public const int SLIDE_HEIGHT = 1080;

    private const string SLIDE_TEST_ID = "render-slide";
    private const string SLIDE_SETTLED = "() => { const slide = document.querySelector('[data-testid=render-slide]'); return !!slide && (slide.dataset.ready === 'true' || slide.hasAttribute('data-error')); }";

    // A slide of the deck, on the render route, with a Preview token. The card path is left as it is.
    public async Task<RenderedSlide> RenderSlideAsync(string deckId, int slide, CancellationToken ct)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_options.Timeout);
        var deadlineToken = deadline.Token;
        var render = RenderSlideInSlotAsync(deckId, slide, deadlineToken);
        _ = render.ContinueWith(_ => deadline.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (deadlineToken.Register(() => stopped.TrySetResult()))
        {
            if (await Task.WhenAny(render, stopped.Task) == render && !(render.IsCanceled && deadlineToken.IsCancellationRequested))
            {
                return await render;
            }
        }

        ct.ThrowIfCancellationRequested();
        throw new System.TimeoutException(TIMED_OUT);
    }

    private async Task<RenderedSlide> RenderSlideInSlotAsync(string deckId, int slide, CancellationToken ct)
    {
        await _pages.WaitAsync(ct);
        try
        {
            var chromium = await browser.GetAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                return await RenderSlidePageAsync(chromium, deckId, slide, ct);
            }
            catch (PlaywrightException) when (!chromium.IsConnected && !ct.IsCancellationRequested)
            {
                var restarted = await browser.GetAsync(ct);
                ct.ThrowIfCancellationRequested();
                return await RenderSlidePageAsync(restarted, deckId, slide, ct);
            }
        }
        catch (PlaywrightException)
        {
            ct.ThrowIfCancellationRequested();
            throw Failed(PAGE_ERROR);
        }
        finally
        {
            ReleaseSlot();
        }
    }

    private async Task<RenderedSlide> RenderSlidePageAsync(IBrowser chromium, string deckId, int slide, CancellationToken ct)
    {
        var origin = BaseUrl();
        var token = tokens.Issue(deckId, RenderPurpose.Preview);
        var url = new Uri(origin, $"render/{Uri.EscapeDataString(deckId)}/{slide}#token={Uri.EscapeDataString(token)}");
        await using var context = await chromium.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = SLIDE_WIDTH, Height = SLIDE_HEIGHT },
            ExtraHTTPHeaders = new Dictionary<string, string> { [RenderSessions.HEADER] = token },
        });
        await using var closeOnDeadline = ct.Register(() => _ = context.CloseAsync());
        context.SetDefaultTimeout((float)_options.Timeout.TotalMilliseconds);
        await context.RouteAsync("**/*", route => SameOrigin(route.Request.Url, origin) ? route.ContinueAsync() : route.AbortAsync());

        var imageFailed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = await context.NewPageAsync();
        page.RequestFailed += (_, request) => FailOnImage(imageFailed, request, ok: false);
        page.Response += (_, response) => FailOnImage(imageFailed, response.Request, response.Ok);
        await page.GotoAsync(url.ToString());
        var settled = page.WaitForFunctionAsync(SLIDE_SETTLED);
        if (await Task.WhenAny(settled, imageFailed.Task) == imageFailed.Task)
        {
            throw Failed(await imageFailed.Task);
        }

        await settled;
        var frame = page.GetByTestId(SLIDE_TEST_ID);
        var error = await frame.GetAttributeAsync("data-error");
        if (error != null)
        {
            throw Failed(error.StartsWith(FAILED, StringComparison.Ordinal) ? error[FAILED.Length..] : error);
        }

        var png = await frame.ScreenshotAsync(new LocatorScreenshotOptions { Type = ScreenshotType.Png });
        return new RenderedSlide(png, ParseChecks(await frame.GetAttributeAsync("data-checks")));
    }

    // The data-checks attribute: absent means none measured; text that isn't a list of findings is a failed render.
    public static IReadOnlyList<SlideFinding> ParseChecks(string? json)
    {
        if (json == null)
        {
            return [];
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<SlideFinding>>(json, FindingJson.Options);
            if (list == null || list.Any(f => f == null || f.Where == null || f.Message == null))
            {
                throw Failed("checks unreadable");
            }

            return list;
        }
        catch (JsonException)
        {
            throw Failed("checks unreadable");
        }
    }

    // Measures an uploaded font in a context of its own: no network, a CSP that allows only data: fonts, and a deadline
    // (MEASURE_TIMEOUT, covering the slot, the browser start and the page) after which it is a TimeoutException.
    public async Task<double> MeasureFontAsync(byte[] woff2, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(MEASURE_TIMEOUT);
        try
        {
            await _pages.WaitAsync(deadline.Token);
            try
            {
                var chromium = await browser.GetAsync(deadline.Token);
                try
                {
                    return await MeasurePageAsync(chromium, woff2, deadline.Token);
                }
                catch (PlaywrightException) when (!chromium.IsConnected && !deadline.IsCancellationRequested)
                {
                    return await MeasurePageAsync(await browser.GetAsync(deadline.Token), woff2, deadline.Token);
                }
            }
            finally
            {
                ReleaseSlot();
            }
        }
        catch (Exception e) when (e is OperationCanceledException or PlaywrightException && !ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new System.TimeoutException(MEASURE_TIMED_OUT);
        }
        catch (PlaywrightException e) when (e.Message.Contains("font not loaded", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The font could not be decoded");
        }
        catch (PlaywrightException)
        {
            throw new RendererUnavailableException("The font could not be measured");
        }
    }

    private static async Task<double> MeasurePageAsync(IBrowser chromium, byte[] woff2, CancellationToken ct)
    {
        await using var context = await chromium.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1200, Height = 400 }, Offline = true, ServiceWorkers = ServiceWorkerPolicy.Block });
        await using var closeOnDeadline = ct.Register(() => _ = context.CloseAsync());
        await context.RouteAsync("**/*", route => route.AbortAsync());
        var page = await context.NewPageAsync();
        await page.SetContentAsync(FontMeasurePage.Html(woff2));
        var width = await page.EvaluateAsync<double>(FontMeasurePage.MEASURE);
        return FontMeasurePage.Raw(width);
    }

    public void Dispose() => _pages.Dispose();

    // The deadline covers the whole render: waiting for a page slot, starting the browser and the page itself. On the
    // deadline the context is closed, so the abandoned render frees its slot straight away.
    private async Task<byte[]> RenderAsync(string? deckId, CancellationToken ct)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_options.Timeout);
        var deadlineToken = deadline.Token;
        var render = RenderInSlotAsync(deckId, deadlineToken);
        _ = render.ContinueWith(_ => deadline.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (deadlineToken.Register(() => stopped.TrySetResult()))
        {
            // A render cancelled by the deadline can finish before the deadline's own signal; it is still a timeout.
            if (await Task.WhenAny(render, stopped.Task) == render && !(render.IsCanceled && deadlineToken.IsCancellationRequested))
            {
                return await render;
            }
        }
        ct.ThrowIfCancellationRequested();
        throw new System.TimeoutException(TIMED_OUT);
    }

    // Any other Playwright failure (a refused or failed navigation, a crashed page, the retry failing too) becomes
    // "Rendering failed: page error". Its message and call log are never passed on, not even as the inner exception,
    // because Playwright puts the navigated URL in them, and that carries the render token in its fragment.
    private async Task<byte[]> RenderInSlotAsync(string? deckId, CancellationToken ct)
    {
        await _pages.WaitAsync(ct);
        try
        {
            var chromium = await browser.GetAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                return await RenderPageAsync(chromium, deckId, ct);
            }
            catch (PlaywrightException) when (!chromium.IsConnected && !ct.IsCancellationRequested)
            {
                var restarted = await browser.GetAsync(ct);
                ct.ThrowIfCancellationRequested();
                return await RenderPageAsync(restarted, deckId, ct);
            }
        }
        catch (PlaywrightException)
        {
            // Past the deadline this is the timeout, not a failure. Rethrow it as cancellation (which the caller
            // reports as "Rendering timed out") and never let Playwright's message, with the token's URL, escape.
            ct.ThrowIfCancellationRequested();
            throw Failed(PAGE_ERROR);
        }
        finally
        {
            ReleaseSlot();
        }
    }

    // A render abandoned on its deadline can still finish after the app has disposed the engine; its slot no longer
    // matters then.
    private void ReleaseSlot()
    {
        try
        {
            _pages.Release();
        }
        catch (ObjectDisposedException)
        {
            // Disposed at shutdown: nothing waits for the slot any more.
        }
    }

    private async Task<byte[]> RenderPageAsync(IBrowser chromium, string? deckId, CancellationToken ct)
    {
        var origin = BaseUrl();
        var headers = new Dictionary<string, string>();
        Uri url;
        if (deckId == null)
        {
            url = new Uri(origin, "card/generic");
        }
        else
        {
            var token = tokens.Issue(deckId, RenderPurpose.Card);
            headers[RenderSessions.HEADER] = token;
            url = new Uri(origin, $"card/{Uri.EscapeDataString(deckId)}#token={Uri.EscapeDataString(token)}");
        }

        await using var context = await chromium.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = WIDTH, Height = HEIGHT },
            ExtraHTTPHeaders = headers,
        });
        await using var closeOnDeadline = ct.Register(() => _ = context.CloseAsync());
        context.SetDefaultTimeout((float)_options.Timeout.TotalMilliseconds);
        await context.RouteAsync("**/*", route => SameOrigin(route.Request.Url, origin) ? route.ContinueAsync() : route.AbortAsync());

        var imageFailed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = await context.NewPageAsync();
        page.RequestFailed += (_, request) => FailOnImage(imageFailed, request, ok: false);
        page.Response += (_, response) => FailOnImage(imageFailed, response.Request, response.Ok);

        await page.GotoAsync(url.ToString());
        var settled = page.WaitForFunctionAsync(SETTLED);
        if (await Task.WhenAny(settled, imageFailed.Task) == imageFailed.Task)
        {
            throw Failed(await imageFailed.Task);
        }
        await settled;
        var card = page.GetByTestId(CARD_TEST_ID);
        var error = await card.GetAttributeAsync("data-error");
        if (error != null)
        {
            throw Failed(error);
        }
        return await card.ScreenshotAsync(new LocatorScreenshotOptions { Type = ScreenshotType.Png });
    }

    private static void FailOnImage(TaskCompletionSource<string> imageFailed, IRequest request, bool ok)
    {
        if (!ok && request.ResourceType == IMAGE)
        {
            imageFailed.TrySetResult($"{IMAGE} {new Uri(request.Url).Segments[^1]}");
        }
    }

    private static RenderingFailedException Failed(string text) => new(text);

    private static bool SameOrigin(string url, Uri origin) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && Uri.Compare(uri, origin, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;

    // SLIDEWORKS_RENDER_URL, else the address Kestrel actually listens on (port 0 under tests is resolved by then),
    // with a wildcard host turned into IPv4 loopback ([::] too: Kestrel's dual-mode socket accepts IPv4, and a container
    // may have IPv6 off).
    private Uri BaseUrl()
    {
        if (_options.BaseUrl != null)
        {
            return new Uri(_options.BaseUrl.GetLeftPart(UriPartial.Authority));
        }
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        var address = addresses.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) ?? addresses.FirstOrDefault()
            ?? throw new RendererUnavailableException($"The app has no address for the card renderer; set {RenderOptions.URL_SETTING}");
        var loopback = address
            .Replace("://+", "://127.0.0.1", StringComparison.Ordinal)
            .Replace("://*", "://127.0.0.1", StringComparison.Ordinal)
            .Replace("://0.0.0.0", "://127.0.0.1", StringComparison.Ordinal)
            .Replace("://[::]", "://127.0.0.1", StringComparison.Ordinal);
        return new Uri(new Uri(loopback).GetLeftPart(UriPartial.Authority));
    }
}
