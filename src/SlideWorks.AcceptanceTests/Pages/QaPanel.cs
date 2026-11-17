namespace SlideWorks.AcceptanceTests.Pages;

// The Questions panel in the presenter window (SW-9 Q9), with its settings strip and reactions counter. Q, or the tool
// bar's Questions tool, opens and closes it; it starts closed (Root has a count of 0). One member for every id in
// src/SlideWorks.Client/src/qa/testIds.ts under QA_PANEL_TEST_IDS, the panel's per-row, per-action and per-reaction
// ids, and QA_SETTINGS_STRIP_TEST_IDS; add one there and here together. Per-question members take the question's
// number (1-based); per-reaction members take the emoji's wire name ("thumbsUp", "heart", "laugh", "wow", "clap").
public sealed class QaPanel(IPage page)
{
    public ILocator Root => page.GetByTestId("qa-panel");

    // The tool bar's "Questions" tool (key Q), pressed while the panel is open. It sits outside the panel.
    public ILocator Tool => page.GetByTestId("presenter-tool-questions");

    public ILocator Tabs => Root.GetByTestId("qa-panel-tabs");

    // "Open 3"
    public ILocator TabOpen => Root.GetByTestId("qa-panel-tab-open");

    // "To review 1", shown only when review is on or something is waiting.
    public ILocator TabReview => Root.GetByTestId("qa-panel-tab-review");

    // The mark on "To review" while it isn't empty.
    public ILocator TabReviewMark => Root.GetByTestId("qa-panel-tab-review-mark");

    // "Answered 1"
    public ILocator TabAnswered => Root.GetByTestId("qa-panel-tab-answered");

    // "Hidden 1"
    public ILocator TabHidden => Root.GetByTestId("qa-panel-tab-hidden");

    // "Top ▾" or "Newest ▾", and its two options once it opens as a menu.
    public ILocator Sort => Root.GetByTestId("qa-panel-sort");

    public ILocator SortTop => page.GetByTestId("qa-panel-sort-top");

    public ILocator SortNewest => page.GetByTestId("qa-panel-sort-newest");

    public ILocator Rows => Root.GetByTestId("qa-panel-rows");

    // "No open questions. They appear here as the room asks."
    public ILocator EmptyOpen => Root.GetByTestId("qa-panel-empty-open");

    // "Questions are closed. Nothing new can be asked."
    public ILocator Closed => Root.GetByTestId("qa-panel-closed");

    // "Nothing waiting for approval."
    public ILocator EmptyReview => Root.GetByTestId("qa-panel-empty-review");

    // "Nothing answered yet."
    public ILocator EmptyAnswered => Root.GetByTestId("qa-panel-empty-answered");

    // "Nothing hidden."
    public ILocator EmptyHidden => Root.GetByTestId("qa-panel-empty-hidden");

    // "Questions start when you go live."
    public ILocator NotLive => Root.GetByTestId("qa-panel-not-live");

    // A refused call's message; absent (count 0) when nothing was refused.
    public ILocator Error => Root.GetByTestId("qa-panel-error");

    // The reactions counter: "👍 3  ❤️ 1", or "No reactions yet", or "Reactions off".
    public ILocator Counter => Root.GetByTestId("qa-panel-counter");

    public ILocator CounterEmpty => Root.GetByTestId("qa-panel-counter-empty");

    public ILocator CounterOff => Root.GetByTestId("qa-panel-counter-off");

    // One emoji's total in the counter.
    public ILocator Count(string emoji) => Root.GetByTestId($"qa-panel-count-{emoji}");

    // Question {number}'s row, its text, its meta ("Priya · 12 min · On screen now") and its votes.
    public ILocator Row(int number) => Root.GetByTestId($"qa-panel-row-{number}");

    public ILocator RowText(int number) => Root.GetByTestId($"qa-panel-row-text-{number}");

    public ILocator RowMeta(int number) => Root.GetByTestId($"qa-panel-row-meta-{number}");

    public ILocator RowVotes(int number) => Root.GetByTestId($"qa-panel-row-votes-{number}");

    // Question {number}'s buttons, each named with its question ("Hide: How do you split a big change?"); each is present
    // only in the states that allow it.
    public ILocator Approve(int number) => ActionButton("approve", number);

    public ILocator Hide(int number) => ActionButton("hide", number);

    public ILocator Restore(int number) => ActionButton("restore", number);

    // "Answered"
    public ILocator Answer(int number) => ActionButton("answer", number);

    public ILocator Reopen(int number) => ActionButton("reopen", number);

    // "Show on screen" or "Show on Q&A slide"
    public ILocator Show(int number) => ActionButton("show", number);

    // "Take off screen"
    public ILocator Unshow(int number) => ActionButton("unshow", number);

    // The settings strip: the deck's four toggles and the switch of the slide the presenter is on.
    public ILocator SettingsStrip => Root.GetByTestId("qa-settings-strip");

    // "Questions open|closed"
    public ILocator SettingsQuestions => Root.GetByTestId("qa-settings-strip-questions");

    // "Review first|off"
    public ILocator SettingsReview => Root.GetByTestId("qa-settings-strip-review");

    // "Names allowed|off"
    public ILocator SettingsNames => Root.GetByTestId("qa-settings-strip-names");

    // "Reactions on|off"
    public ILocator SettingsReactions => Root.GetByTestId("qa-settings-strip-reactions");

    // This slide's switch for questions, and its reason when the deck has questions off.
    public ILocator SlideQuestions => Root.GetByTestId("qa-settings-strip-slide-questions");

    public ILocator SlideQuestionsReason => Root.GetByTestId("qa-settings-strip-slide-questions-reason");

    // This slide's switch for reactions, and its reason when the deck has reactions off.
    public ILocator SlideReactions => Root.GetByTestId("qa-settings-strip-slide-reactions");

    public ILocator SlideReactionsReason => Root.GetByTestId("qa-settings-strip-slide-reactions-reason");

    // A failed settings change's message; the control goes back.
    public ILocator SettingsError => Root.GetByTestId("qa-settings-strip-error");

    private ILocator ActionButton(string action, int number) => Root.GetByTestId($"qa-panel-{action}-{number}");
}
