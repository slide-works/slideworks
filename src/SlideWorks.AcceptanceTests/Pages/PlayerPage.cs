using System.Text.Json;

namespace SlideWorks.AcceptanceTests.Pages;

// The player at /d/{id}/{slug}, open to anonymous viewers. /d/{id} (or an old slug) moves to the deck's own slug,
// keeping the #slide.step hash.
public sealed class PlayerPage(IPage page)
{
    // Everything inside a slide's frame that falls outside it, or below the top of the footer on a slide that has one:
    // each text run, image and svg, in canvas pixels (1920×1080 before scaling). The part tracker, footer and progress
    // bar are the frame's own chrome and are left out. An image that hasn't loaded counts too.
    private const string OVERFLOW_PROBE =
        """
        slide => {
          const frame = slide.querySelector('[data-testid="slide-frame"]');
          const box = frame.getBoundingClientRect();
          const scale = box.width / 1920;
          const chrome = ['part-tracker', 'slide-footer', 'slide-progress'];
          const isChrome = (el) => chrome.some((id) => el.closest(`[data-testid="${id}"]`));
          const footer = frame.querySelector('[data-testid="slide-footer"]');
          const floor = footer ? (footer.getBoundingClientRect().top - box.top) / scale : 1080;
          const found = [];
          const check = (what, rect) => {
            if (rect.width === 0 && rect.height === 0) return;
            const left = (rect.left - box.left) / scale;
            const top = (rect.top - box.top) / scale;
            const right = (rect.right - box.left) / scale;
            const bottom = (rect.bottom - box.top) / scale;
            if (left < -0.5 || top < -0.5 || right > 1920.5 || bottom > floor + 0.5) {
              found.push(`${what} at x ${left.toFixed(1)}-${right.toFixed(1)}, y ${top.toFixed(1)}-${bottom.toFixed(1)} (floor ${floor.toFixed(1)})`);
            }
          };
          const walker = document.createTreeWalker(frame, NodeFilter.SHOW_TEXT);
          for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            const text = node.textContent.trim();
            if (!text || isChrome(node.parentElement)) continue;
            const range = document.createRange();
            range.selectNodeContents(node);
            for (const rect of range.getClientRects()) check(`text "${text.slice(0, 40)}"`, rect);
          }
          for (const el of frame.querySelectorAll('img, svg')) {
            if (isChrome(el) || el.parentElement.closest('svg')) continue;
            const tag = el.tagName.toLowerCase();
            check(tag, el.getBoundingClientRect());
            if (tag === 'img' && !(el.complete && el.naturalWidth > 0)) found.push(`img not loaded: ${el.getAttribute('src')}`);
          }
          return found;
        }
        """;

    private const string FIT_PROBE =
        """
        canvas => {
          const box = canvas.getBoundingClientRect();
          return {
            width: box.width,
            height: box.height,
            left: box.left,
            right: box.right,
            scrollWidth: document.documentElement.scrollWidth,
            viewportWidth: window.innerWidth,
          };
        }
        """;

    // touchstart, then touchend 200 px to the left, with one finger, as a phone sends a swipe.
    private const string SWIPE_LEFT =
        """
        stage => {
          const box = stage.getBoundingClientRect();
          const y = box.top + box.height / 2;
          const at = (x) => new Touch({ identifier: 1, target: stage, clientX: x, clientY: y });
          const start = at(box.left + box.width * 0.8);
          const end = at(box.left + box.width * 0.8 - 200);
          stage.dispatchEvent(new TouchEvent('touchstart', { bubbles: true, cancelable: true, touches: [start], changedTouches: [start] }));
          stage.dispatchEvent(new TouchEvent('touchend', { bubbles: true, cancelable: true, touches: [], changedTouches: [end] }));
        }
        """;

    public static string PathFor(string deckId, string? slug = null) =>
        slug is null ? $"/d/{deckId}" : $"/d/{deckId}/{slug}";

    public ILocator Root => page.GetByTestId("player-page");

    public ILocator Player => page.GetByTestId("player");

    // The wrapper around the player while it follows a presentation (/d/{id}/{slug}?present); absent on the plain player.
    public ILocator AudienceMode => page.GetByTestId("audience-mode");

    // The black cover over the stage while the presentation is blacked out.
    public ILocator Blackout => page.GetByTestId("blackout");

    // The bar under the stage; absent while a presenter or phone drives the audience window.
    public ILocator Controls => page.GetByTestId("player-controls");

    public ILocator NotFound => page.GetByTestId("player-not-found");

    public ILocator SignIn => page.GetByTestId("player-sign-in");

    public ILocator Error => page.GetByTestId("player-error");

    // The 1920×1080 canvas, scaled to fit the stage.
    public ILocator Canvas => page.GetByTestId("player-canvas");

    public ILocator Stage => page.GetByTestId("player-stage");

    // "n / total" in the control bar.
    public ILocator Count => page.GetByTestId("player-count");

    // Slide {number}, counted from 1. Every slide stays rendered; only the current one is visible.
    public ILocator Slide(int number) => page.GetByTestId($"slide-{number}");

    // The layer slide {number} sits in on the canvas; its data-active is "true" while it shows.
    public ILocator Layer(int number) => page.GetByTestId($"player-slide-{number}");

    // The owner's live chip in the control bar.
    public LiveChip LiveChip => new(page);

    // The join slide's parts on slide {number} (SW-5 J6): the kicker, the code as "482 913", the address and the QR code.
    public ILocator JoinSlide(int number) => Slide(number).GetByTestId("layout-join");

    public ILocator JoinKicker(int number) => Slide(number).GetByTestId("join-kicker");

    public ILocator JoinCode(int number) => Slide(number).GetByTestId("join-code");

    public ILocator JoinHost(int number) => Slide(number).GetByTestId("join-host");

    public ILocator JoinQr(int number) => Slide(number).GetByTestId("join-qr");

    // "37 joined": only while live, and absent when the slide's count slot is hidden.
    public ILocator JoinCount(int number) => Slide(number).GetByTestId("join-count");

    // The join bar the frame draws on slide {number} while live and the deck's join bar is on: a small QR and the code.
    public ILocator JoinBar(int number) => Slide(number).GetByTestId("join-bar");

    public ILocator JoinBarCode(int number) => JoinBar(number).GetByTestId("join-bar-code");

    public ILocator JoinBarQr(int number) => JoinBar(number).GetByTestId("join-bar-qr");

    public ILocator Image(int number) => Slide(number).GetByTestId("image-picture");

    public ILocator Bullets(int number) => Slide(number).GetByTestId("bullets-item");

    // The bullet points of slide {number} revealed so far: hidden ones keep their space but aren't visible.
    public ILocator RevealedBullets(int number) => Bullets(number).Filter(new LocatorFilterOptions { Visible = true });

    public async Task GotoAsync(string deckId, string? slug = null, string hash = "") =>
        await page.GotoAsync(PathFor(deckId, slug) + hash);

    // The --sw-accent custom property on slide {number}'s root, where the deck's look is set (L10).
    public async Task<string> AccentAsync(int number) =>
        await Slide(number).EvaluateAsync<string>("e => getComputedStyle(e).getPropertyValue('--sw-accent').trim()");

    // Waits until slide {number}'s --sw-accent is {accent} (compared without case), without reloading the page.
    public async Task WaitForAccentAsync(int number, string accent) =>
        await page.WaitForFunctionAsync(
            "([id, accent]) => { const e = document.querySelector(`[data-testid=\"${id}\"]`); "
            + "return e !== null && getComputedStyle(e).getPropertyValue('--sw-accent').trim().toLowerCase() === accent; }",
            new object[] { $"slide-{number}", accent.ToLowerInvariant() });

    // Waits in the browser until slide {number}'s image has loaded and decoded, and fails if it can't (a broken image
    // rejects decode()). The overflow probe counts an image that hasn't loaded, so call this before probing.
    public async Task ImageLoadedAsync(int number) =>
        (await Image(number).EvaluateAsync<int>("img => img.decode().then(() => img.naturalWidth)")).ShouldBeGreaterThan(0);

    // What slide {number} draws outside its frame or past its footer; empty when it fits.
    public Task<string[]> OverflowAsync(int number) => Slide(number).EvaluateAsync<string[]>(OVERFLOW_PROBE);

    // The canvas's drawn box, the document's scroll width and the viewport's width, in CSS pixels.
    public async Task<CanvasFit> FitAsync()
    {
        var fit = await Canvas.EvaluateAsync<JsonElement>(FIT_PROBE);
        double Read(string name) => fit.GetProperty(name).GetDouble();
        return new CanvasFit(Read("width"), Read("height"), Read("left"), Read("right"), Read("scrollWidth"), Read("viewportWidth"));
    }

    public Task SwipeLeftAsync() => Stage.EvaluateAsync(SWIPE_LEFT);

    // Waits for the theme's web fonts, so text is measured in the face it is drawn in.
    public Task FontsReadyAsync() => page.EvaluateAsync("() => document.fonts.ready.then(() => true)");

    public sealed record CanvasFit(double Width, double Height, double Left, double Right, double ScrollWidth, double ViewportWidth);
}
