namespace SlideWorks.AcceptanceTests.Pages;

// The poll slide (SW-6 V7) inside one slide: pass the slide's locator, such as PlayerPage.Slide(4), since every slide of
// the player stays rendered and a deck may hold more than one poll. The same slide shows in the player, the audience
// window, the presenter window and the deck page's thumbnails. One member for every id in
// src/SlideWorks.Client/src/polls/testIds.ts (POLL_SLIDE_TEST_IDS and the per-option ids); add one there and here
// together. Per-option members take the option's 0-based position, the same index as an answer's picks.
public sealed class PollSlide(ILocator slide)
{
    public ILocator Root => slide.GetByTestId("poll-slide");

    // The join pill, top right, while the deck is live.
    public ILocator Join => slide.GetByTestId("poll-slide-join");

    // "Poll · Pick one"
    public ILocator Kind => slide.GetByTestId("poll-slide-kind");

    public ILocator Question => slide.GetByTestId("poll-slide-question");

    public ILocator Rows => slide.GetByTestId("poll-slide-rows");

    // "Not open yet", "Open" or "Closed"
    public ILocator State => slide.GetByTestId("poll-slide-state");

    // "38 answers"
    public ILocator Answered => slide.GetByTestId("poll-slide-answered");

    // "Results show when the poll closes", while the room can't see the counts.
    public ILocator HiddenNote => slide.GetByTestId("poll-slide-hidden-note");

    // "Share of people who picked each. Adds up to more than 100%."
    public ILocator ManyNote => slide.GetByTestId("poll-slide-many-note");

    // "Round 2"
    public ILocator Round => slide.GetByTestId("poll-slide-round");

    // Option {position}'s row: its text, its bar, its percentage and, once the answer is shown, its correct mark.
    public ILocator Row(int position) => slide.GetByTestId($"poll-slide-row-{position}");

    public ILocator Bar(int position) => slide.GetByTestId($"poll-slide-bar-{position}");

    public ILocator Percent(int position) => slide.GetByTestId($"poll-slide-percent-{position}");

    public ILocator Correct(int position) => slide.GetByTestId($"poll-slide-correct-{position}");
}
