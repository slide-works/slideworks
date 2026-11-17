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

    public async Task GotoAsync(string code) => await page.GotoAsync(PathFor(code));

    // The join host's form of the link: /{code}.
    public async Task GotoBareAsync(string code) => await page.GotoAsync($"/{code}");

    // Waits until the guest page has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();

    // Waits until the phone shows the slide mirror, after "You're in" has gone.
    public async Task IsFollowingAsync() => await Expect(Following).ToBeVisibleAsync();
}
