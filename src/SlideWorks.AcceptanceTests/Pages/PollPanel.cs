namespace SlideWorks.AcceptanceTests.Pages;

// The poll panel in the presenter window (SW-6 V8). One member for every id in
// src/SlideWorks.Client/src/polls/testIds.ts (POLL_PANEL_TEST_IDS and the per-option and per-round ids); add one there
// and here together. Per-option members take the option's 0-based position; per-round members take the round's number.
// The keys: P opens or closes the poll, R reveals, A shows the answer; New round has no key (D19).
public sealed class PollPanel(IPage page)
{
    public ILocator Root => page.GetByTestId("poll-panel");

    // "No poll on this slide."
    public ILocator Empty => Root.GetByTestId("poll-panel-empty");

    // "Draft", "Open" or "Closed"
    public ILocator State => Root.GetByTestId("poll-panel-state");

    // "round 2"
    public ILocator Round => Root.GetByTestId("poll-panel-round");

    // "Pick one · Live"
    public ILocator Settings => Root.GetByTestId("poll-panel-settings");

    // The answer count.
    public ILocator Answered => Root.GetByTestId("poll-panel-answered");

    // "93% of 41 joined"
    public ILocator Rate => Root.GetByTestId("poll-panel-rate");

    public ILocator Bars => Root.GetByTestId("poll-panel-bars");

    // What the room sees: "The room sees the bars" or "Hidden from the room until …"
    public ILocator Room => Root.GetByTestId("poll-panel-room");

    // The five controls; each is present only in the phases that allow it. Their names include the question.
    public ILocator Open => Root.GetByTestId("poll-panel-open");

    public ILocator Close => Root.GetByTestId("poll-panel-close");

    public ILocator Reveal => Root.GetByTestId("poll-panel-reveal");

    public ILocator ShowAnswer => Root.GetByTestId("poll-panel-show-answer");

    public ILocator NewRound => Root.GetByTestId("poll-panel-new-round");

    // Earlier rounds of this session: "Round 1: 38 answers, kept"
    public ILocator History => Root.GetByTestId("poll-panel-history");

    // A refused call's message; absent (count 0) when nothing was refused.
    public ILocator Error => Root.GetByTestId("poll-panel-error");

    // "P open or close the poll · R reveal · A show answer"
    public ILocator Keys => Root.GetByTestId("poll-panel-keys");

    // Option {position}'s row, its bar, its count and, once the answer is shown, its correct mark.
    public ILocator Row(int position) => Root.GetByTestId($"poll-panel-row-{position}");

    public ILocator Bar(int position) => Root.GetByTestId($"poll-panel-bar-{position}");

    public ILocator Count(int position) => Root.GetByTestId($"poll-panel-count-{position}");

    public ILocator Correct(int position) => Root.GetByTestId($"poll-panel-correct-{position}");

    // Round {number} in the history.
    public ILocator HistoryRound(int number) => Root.GetByTestId($"poll-panel-history-round-{number}");
}
