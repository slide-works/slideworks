using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The presenter window at /decks/{id}/present (P8): signed in, with no Shell chrome. The deck page's Presenter view
// action opens it, and opens the audience window next to it. It drives the presentation with its keys and buttons and
// shows the state the hub sends back. The top bar, the overview and the phone dialog are inside it.
public sealed class PresenterPage(IPage page)
{
    public static string PathFor(string deckId) => $"/decks/{Uri.EscapeDataString(deckId)}/present";

    public ILocator Root => page.GetByTestId("presenter-page");

    // Shown while the window joins the presentation, before it can be driven.
    public ILocator Connecting => page.GetByTestId("presenter-connecting");

    // A refused join or a 404 on the presenter view: not the owner's deck, or no deck.
    public ILocator NotFound => page.GetByTestId("presenter-not-found");

    public ILocator LoadError => page.GetByTestId("presenter-load-error");

    // A failed move or timer call.
    public ILocator Error => page.GetByTestId("presenter-error");

    public ILocator Reconnecting => page.GetByTestId("presenter-reconnecting");

    public ILocator AudienceClosed => page.GetByTestId("presenter-audience-closed");

    public ILocator AudienceReopen => page.GetByTestId("presenter-audience-reopen");

    // Now: the slide the audience sees.
    public ILocator Now => page.GetByTestId("presenter-now");

    public ILocator NowThumb => Now.GetByTestId("presenter-thumb");

    // "8 / 16".
    public ILocator Count => page.GetByTestId("presenter-count");

    // "8 / 16", or "3.2 / 16" on a reveal.
    public ILocator Position => page.GetByTestId("presenter-position");

    public ILocator BlackBadge => page.GetByTestId("presenter-black-badge");

    public ILocator PreviousButton => page.GetByTestId("presenter-previous");

    public ILocator NextButton => page.GetByTestId("presenter-next-button");

    // Next: the next reveal or slide.
    public ILocator Next => page.GetByTestId("presenter-next");

    public ILocator NextThumb => Next.GetByTestId("presenter-thumb");

    // "slide 9", "reveal 2 of 4", "slide 10 · deep dive" or "end of deck".
    public ILocator NextLabel => page.GetByTestId("presenter-next-label");

    // "Skipping deep dive 10: …" or "2 deep dives ahead".
    public ILocator NextNote => page.GetByTestId("presenter-next-note");

    public ILocator Notes => page.GetByTestId("presenter-notes");

    public ILocator NotesBody => page.GetByTestId("presenter-notes-body");

    public ILocator NotesSmaller => page.GetByTestId("presenter-notes-smaller");

    public ILocator NotesLarger => page.GetByTestId("presenter-notes-larger");

    // The tool bar's buttons; the toggles carry aria-pressed.
    public ILocator OverviewTool => page.GetByTestId("presenter-tool-overview");

    public ILocator NotesTool => page.GetByTestId("presenter-tool-notes");

    public ILocator SkipDeepDivesTool => page.GetByTestId("presenter-tool-skip");

    public ILocator BlackoutTool => page.GetByTestId("presenter-tool-black");

    public ILocator PhoneTool => page.GetByTestId("presenter-tool-phone");

    // The top bar.
    public ILocator TopBar => page.GetByTestId("presenter-top-bar");

    public ILocator Part => page.GetByTestId("presenter-part");

    // The timer, with data-state (notStarted, running, paused) and data-tone (plain, amber, red).
    public ILocator Timer => page.GetByTestId("presenter-timer");

    public ILocator TimeLeft => page.GetByTestId("presenter-time-left");

    public ILocator Clock => page.GetByTestId("presenter-clock");

    public ILocator TimerPause => page.GetByTestId("presenter-timer-pause");

    public ILocator TimerReset => page.GetByTestId("presenter-timer-reset");

    // One chip per connected audience window and phone, with data-role (audience or phone).
    public ILocator Chips => page.GetByTestId("presenter-chip");

    // The audience windows' chips ("Audience Chrome on Windows") and the phones' ("Phone Safari on iPhone").
    public ILocator AudienceChips => Chips.Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^Audience") });

    public ILocator PhoneChips => Chips.Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^Phone") });

    // The grey chip shown when no audience window is connected, and its Reopen button.
    public ILocator NoAudienceChip => page.GetByTestId("presenter-no-audience");

    public ILocator TopBarReopenAudience => page.GetByTestId("presenter-reopen-audience");

    public ILocator LiveChip => page.GetByTestId("presenter-live-chip");

    // The Reading in counter's wrapper, just before the live chip (SW-886 K11); src/live/testIds.ts has the ids inside it:
    // the group (named "Reading in: English 31, …"), one count per language drawn ("EN 31") and "+n" past the fourth.
    public ILocator ReadingIn => page.GetByTestId("presenter-reading-in");

    public ILocator ReadingInGroup => ReadingIn.GetByTestId("reading-in");

    public ILocator ReadingInCounts => ReadingIn.GetByTestId("reading-in-count");

    public ILocator ReadingInMore => ReadingIn.GetByTestId("reading-in-more");

    // The owner's live chip inside that wrapper (Go live, the code and joined count, the join bar toggle, End), the same
    // chip the plain player has.
    public LiveChip Live => new(page);

    // A join slide in the Now and Next panels: its code, and the line it shows until the deck is live.
    public ILocator NowJoinCode => Now.GetByTestId("join-code");

    public ILocator NowJoinNotLive => Now.GetByTestId("join-not-live");

    public ILocator NextJoinCode => Next.GetByTestId("join-code");

    public ILocator NextJoinNotLive => Next.GetByTestId("join-not-live");

    // The overview (O), and its button for slide n.
    public ILocator Overview => page.GetByTestId("overview");

    public ILocator OverviewSlide(int number) => page.GetByTestId($"overview-slide-{number}");

    // The phone dialog the Phone tool opens, and the remote link it shows.
    public ILocator PhoneConnect => page.GetByTestId("phone-connect");

    public ILocator PhoneConnectUrl => page.GetByTestId("phone-connect-url");

    public async Task GotoAsync(string deckId) => await page.GotoAsync(PathFor(deckId));

    // Waits until the presenter window has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();

    // Waits until the window has joined and can be driven with its keys.
    public async Task IsLiveAsync() => await Expect(Now).ToBeVisibleAsync();

    // Presses a key in the presenter window, as the presenter does (P8): ArrowRight, Space, b, Home, …
    public async Task PressAsync(string key) => await page.Keyboard.PressAsync(key);

    // Types a slide number and presses Enter: a jump.
    public async Task JumpToAsync(int number)
    {
        foreach (var digit in number.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            await page.Keyboard.PressAsync(new string(digit, 1));
        }

        await page.Keyboard.PressAsync("Enter");
    }
}
