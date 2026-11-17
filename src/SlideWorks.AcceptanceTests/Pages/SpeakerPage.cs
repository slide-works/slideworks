using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The phone's speaker mode at /remote/{id} (P8): signed in, with no Shell chrome. The slide comes first, the notes
// underneath (D6), with Next and Previous buttons, Slides (the slides sheet), Skip deep dives and Blackout.
public sealed class SpeakerPage(IPage page)
{
    public static string PathFor(string deckId) => $"/remote/{Uri.EscapeDataString(deckId)}";

    public ILocator Root => page.GetByTestId("speaker-page");

    // Live, Connecting or Reconnecting; its data-status is the presentation's status (live, connecting, reconnecting).
    public ILocator Status => page.GetByTestId("speaker-status");

    // The timer: a tap pauses or resumes it. data-state is the timer's state, data-tone normal, amber or red.
    public ILocator Timer => page.GetByTestId("speaker-timer");

    // "17:20 left" or "0:42 over", only when the deck has a talk length.
    public ILocator TimeLeft => page.GetByTestId("speaker-time-left");

    // "8 / 16": the current slide and the deck's count.
    public ILocator Count => page.GetByTestId("speaker-count");

    // The current slide's part, when it has one.
    public ILocator Part => page.GetByTestId("speaker-part");

    // The current slide's picture; data-slide and data-step are the position it shows.
    public ILocator Now => page.GetByTestId("speaker-now");

    // "The room sees a black screen", only while blackout is on.
    public ILocator BlackNotice => page.GetByTestId("speaker-black-notice");

    // The next row: a thumbnail, what Next shows ("slide 9", "reveal 2 of 4") and the deep dives it skips or that lie
    // ahead.
    public ILocator NextRow => page.GetByTestId("speaker-next-row");
    public ILocator NextLabel => page.GetByTestId("speaker-next-label");
    public ILocator NextNote => page.GetByTestId("speaker-next-note");

    // The current slide's notes ("Notes for slide n (last known)" while reconnecting).
    public ILocator Notes => page.GetByTestId("speaker-notes");

    public ILocator Next => page.GetByTestId("speaker-next");
    public ILocator Previous => page.GetByTestId("speaker-previous");
    public ILocator Slides => page.GetByTestId("speaker-slides");

    // Toggles; aria-pressed is their state.
    public ILocator Skip => page.GetByTestId("speaker-skip");
    public ILocator Blackout => page.GetByTestId("speaker-blackout");

    public ILocator Reconnecting => page.GetByTestId("speaker-reconnecting");
    public ILocator NotYours => page.GetByTestId("speaker-not-yours");

    // A call that failed ("That didn't reach the talk…").
    public ILocator Error => page.GetByTestId("speaker-error");

    // The slides sheet Slides opens, and its row for slide n (a tap jumps there).
    public ILocator SlidesSheet => page.GetByTestId("slides-sheet");
    public ILocator SlidesSheetRow(int n) => page.GetByTestId($"slides-sheet-row-{n}");

    public async Task GotoAsync(string deckId) => await page.GotoAsync(PathFor(deckId));

    // Waits until speaker mode has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();

    // Waits until the phone has joined the presentation and its buttons work.
    public async Task IsLiveAsync() => await Expect(Status).ToHaveAttributeAsync("data-status", "live");

    // Opens the slides sheet and jumps to slide n.
    public async Task JumpToAsync(int n)
    {
        await Slides.ClickAsync();
        await SlidesSheetRow(n).ClickAsync();
    }
}
