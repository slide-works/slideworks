using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The guest page at /j/{code}, and at /{code} on the join host (SW-5 J5): anyone with a join code, no sign-in. One member
// for every test id in src/SlideWorks.Client/src/guest/testIds.ts (GUEST_TEST_IDS); add one there and here together.
// A code with no session keeps this page and shows the keypad's error (JoinCodePage.Error) inside it.
public sealed class GuestPage(IPage page)
{
    public static string PathFor(string code) => $"/j/{code}";

    // The page's root, present from the first render whatever the API returns.
    public ILocator Root => page.GetByTestId("guest-page");

    public ILocator Loading => page.GetByTestId("guest-loading");

    public ILocator DeckTitle => page.GetByTestId("guest-deck-title");

    public ILocator Presenter => page.GetByTestId("guest-presenter");

    // "You're in", for 2 seconds after the first live state.
    public ILocator Joined => page.GetByTestId("guest-joined");

    // The slide mirror and its card, once live.
    public ILocator Following => page.GetByTestId("guest-following");

    // The slide on screen, and the "n / total" strip under it.
    public ILocator Slide => page.GetByTestId("guest-slide");

    public ILocator Progress => page.GetByTestId("guest-progress");

    // The card below the slide, and the "nothing to answer yet" text that fills it in SW-5.
    public ILocator Activity => page.GetByTestId("guest-activity");

    public ILocator ActivityWaiting => page.GetByTestId("guest-activity-waiting");

    // Before Go live.
    public ILocator Lobby => page.GetByTestId("guest-lobby");

    // After the session ended, and its link to the slides (only when the deck is shared by link).
    public ILocator Ended => page.GetByTestId("guest-ended");

    public ILocator EndedSlides => page.GetByTestId("guest-ended-slides");

    // Live updates can't reach the phone, and its retry button.
    public ILocator Offline => page.GetByTestId("guest-offline");

    public ILocator OfflineRetry => page.GetByTestId("guest-offline-retry");

    // The banner over the last screen while the connection comes back.
    public ILocator Reconnecting => page.GetByTestId("guest-reconnecting");

    // "Guest · Privacy" in the footer, the sheet it opens and its close button.
    public ILocator PrivacyOpen => page.GetByTestId("guest-privacy-open");

    public ILocator PrivacySheet => page.GetByTestId("guest-privacy-sheet");

    public ILocator PrivacyClose => page.GetByTestId("guest-privacy-close");

    // The browser blocks the cookie the guest page needs, and its retry button.
    public ILocator NeedsCookies => page.GetByTestId("guest-needs-cookies");

    public ILocator NeedsCookiesRetry => page.GetByTestId("guest-needs-cookies-retry");

    // The page's own load failure, and its retry button.
    public ILocator Error => page.GetByTestId("guest-error");

    public ILocator ErrorRetry => page.GetByTestId("guest-error-retry");

    // The "Live" or "Offline" chip in the following header.
    public ILocator Chip => page.GetByTestId("guest-chip");

    // Shown in place of the slide while the session is live and the presenter has not published a position.
    public ILocator WaitingForPresenter => page.GetByTestId("guest-waiting-for-presenter");

    // SW-883: the language chip in the header (and the landscape overlay), the sheet it opens, and every language in it.
    public ILocator LanguageChip => page.GetByTestId("language-chip");

    public ILocator LanguageSheet => page.GetByTestId("language-sheet");

    public ILocator LanguageOptions => page.GetByTestId("language-option");

    // The sheet's row for one language, by the name it shows first (its own name, such as "ไทย").
    public ILocator LanguageOption(string name) =>
        LanguageOptions.Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(name)}") });

    // The one-time "Showing the slides in ..." banner and its Change button.
    public ILocator LanguageBanner => page.GetByTestId("language-banner");

    public ILocator LanguageBannerChange => page.GetByTestId("language-banner-change");

    // "n lines on this slide are in English", under the slide.
    public ILocator FallbackNote => page.GetByTestId("fallback-note");

    // "Couldn't switch language. Try again."
    public ILocator LanguageError => page.GetByTestId("language-error");

    // Landscape: the overlay a tap on the slide shows, its Full screen button (Android) and that button's error.
    public ILocator LandscapeOverlay => page.GetByTestId("landscape-overlay");

    public ILocator LandscapeFullscreen => page.GetByTestId("landscape-fullscreen");

    public ILocator LandscapeFullscreenError => page.GetByTestId("landscape-fullscreen-error");

    // The one-time "Turn sideways" hint in portrait, and its close button.
    public ILocator PortraitHint => page.GetByTestId("portrait-hint");

    public ILocator PortraitHintClose => page.GetByTestId("portrait-hint-close");

    // Opens a link as typed or scanned, in full (the join host's address is another origin from the app's).
    public async Task GotoUrlAsync(string url) => await page.GotoAsync(url);

    public async Task GotoAsync(string code) => await page.GotoAsync(PathFor(code));

    // The join host's form of the link: /{code}.
    public async Task GotoBareAsync(string code) => await page.GotoAsync($"/{code}");

    // Waits until the guest page has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();

    // Waits until the phone shows the slide mirror, after "You're in" has gone.
    public async Task IsFollowingAsync() => await Expect(Following).ToBeVisibleAsync();
}
