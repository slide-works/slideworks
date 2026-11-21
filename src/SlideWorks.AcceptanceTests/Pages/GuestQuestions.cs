namespace SlideWorks.AcceptanceTests.Pages;

// The Questions tab, the tab bar and the reaction row on a guest's phone (SW-9 Q8), inside GuestPage.Following. One member
// for every guest id in src/SlideWorks.Client/src/qa/testIds.ts (QA_TABS_TEST_IDS, QA_REACTION_ROW_TEST_IDS, the reaction
// buttons, QA_LIST_TEST_IDS, the per-question ids, QA_ASK_SHEET_TEST_IDS and QA_POLL_OPENED_TEST_IDS); add one there and
// here together. Per-question members take the question's 1-based number, as on the wire.
public sealed class GuestQuestions(IPage page)
{
    // The tab bar at the bottom (qa/guest/GuestTabs), absent when questions are off.
    public ILocator TabBar => page.GetByTestId("qa-tabs");

    // "Slide"
    public ILocator SlideTab => page.GetByTestId("qa-tab-slide");

    // The green dot on the Slide tab while a poll is open and Questions is showing: "A poll is open"
    public ILocator SlideTabDot => page.GetByTestId("qa-tab-slide-dot");

    // "Questions", named "Questions, 6 open"
    public ILocator QuestionsTab => page.GetByTestId("qa-tab-questions");

    // The count of open questions on the Questions tab: "6"
    public ILocator QuestionsBadge => page.GetByTestId("qa-tab-questions-badge");

    // The tab panels
    public ILocator SlidePanel => page.GetByTestId("qa-tab-panel-slide");

    public ILocator QuestionsPanel => page.GetByTestId("qa-tab-panel-questions");

    // The reaction row under the slide (qa/guest/ReactionRow), absent when reactions are off.
    // The group named "Reactions", greyed while cooling
    public ILocator ReactionRow => page.GetByTestId("qa-reaction-row");

    // A failed tap's message
    public ILocator ReactionError => page.GetByTestId("qa-reaction-row-error");

    // The emoji that pops up from a tapped button; absent with reduced motion
    public ILocator ReactionPop => page.GetByTestId("qa-reaction-pop");

    // One reaction button, by its wire name: thumbsUp, heart, laugh, wow or clap.
    public ILocator Reaction(string emoji) => page.GetByTestId($"qa-reaction-{emoji}");

    // The Questions tab's list (qa/guest/QuestionList, QuestionsEmpty, AskBar).
    public ILocator List => page.GetByTestId("qa-list");

    // "Questions"
    public ILocator ListHeading => page.GetByTestId("qa-list-heading");

    // "6 open"
    public ILocator ListOpenCount => page.GetByTestId("qa-list-open-count");

    // The sort's group, with "Top" and "New"
    public ILocator ListSort => page.GetByTestId("qa-list-sort");

    public ILocator ListSortTop => page.GetByTestId("qa-list-sort-top");

    public ILocator ListSortNew => page.GetByTestId("qa-list-sort-new");

    // The open questions, and the guest's own waiting and hidden ones pinned above them
    public ILocator ListRows => page.GetByTestId("qa-list-rows");

    // "Answered · 2"
    public ILocator ListAnswered => page.GetByTestId("qa-list-answered");

    // One question inside the answered group (1-based number).
    public ILocator AnsweredQuestion(int number) => ListAnswered.GetByTestId($"qa-question-{number}");

    public ILocator ListAnsweredHeading => page.GetByTestId("qa-list-answered-heading");

    // While the first read is on its way
    public ILocator ListLoading => page.GetByTestId("qa-list-loading");

    // "Couldn't load the questions."
    public ILocator ListFailed => page.GetByTestId("qa-list-failed");

    // "Try again"
    public ILocator ListFailedRetry => page.GetByTestId("qa-list-failed-retry");

    // A failed upvote: the hook's error under the list
    public ILocator ListError => page.GetByTestId("qa-list-error");

    // QuestionsEmpty: "No questions yet" / "Ask the first one. Other people can upvote it, and <presenter> sees which
    // questions the room wants answered."
    public ILocator ListEmpty => page.GetByTestId("qa-list-empty");

    public ILocator ListEmptyTitle => page.GetByTestId("qa-list-empty-title");

    public ILocator ListEmptyText => page.GetByTestId("qa-list-empty-text");

    // "<presenter> has closed questions. You can still upvote."
    public ILocator ListClosed => page.GetByTestId("qa-list-closed");

    // "Questions are off on this slide. You can still upvote."
    public ILocator ListOffHere => page.GetByTestId("qa-list-off-here");

    // AskBar, which stays in view under the list
    public ILocator AskBar => page.GetByTestId("qa-ask-bar");

    // "Ask a question"
    public ILocator Ask => page.GetByTestId("qa-ask-bar-ask");

    // One question in the list, by its 1-based number, and its parts (qa/guest/QuestionRow).
    public ILocator Question(int number) => page.GetByTestId($"qa-question-{number}");

    public ILocator QuestionText(int number) => page.GetByTestId($"qa-question-text-{number}");

    public ILocator QuestionMeta(int number) => page.GetByTestId($"qa-question-meta-{number}");

    public ILocator QuestionUpvote(int number) => page.GetByTestId($"qa-question-upvote-{number}");

    public ILocator QuestionVotes(int number) => page.GetByTestId($"qa-question-votes-{number}");

    public ILocator QuestionNoVote(int number) => page.GetByTestId($"qa-question-no-vote-{number}");

    public ILocator QuestionAnswered(int number) => page.GetByTestId($"qa-question-answered-{number}");

    public ILocator QuestionYou(int number) => page.GetByTestId($"qa-question-you-{number}");

    public ILocator QuestionWaiting(int number) => page.GetByTestId($"qa-question-waiting-{number}");

    public ILocator QuestionOnScreen(int number) => page.GetByTestId($"qa-question-on-screen-{number}");

    public ILocator QuestionHidden(int number) => page.GetByTestId($"qa-question-hidden-{number}");

    // The ask sheet over the list (qa/guest/AskSheet).
    public ILocator AskSheet => page.GetByTestId("qa-ask-sheet");

    // "Ask a question"
    public ILocator AskSheetTitle => page.GetByTestId("qa-ask-sheet-title");

    // "✕"
    public ILocator AskSheetClose => page.GetByTestId("qa-ask-sheet-close");

    // The one-line text box: "What would you like <presenter> to answer?"
    public ILocator AskSheetText => page.GetByTestId("qa-ask-sheet-text");

    // "One question at a time works best"
    public ILocator AskSheetHint => page.GetByTestId("qa-ask-sheet-hint");

    // "42 / 200"
    public ILocator AskSheetCounter => page.GetByTestId("qa-ask-sheet-counter");

    // The name row, absent when names are off
    public ILocator AskSheetNameRow => page.GetByTestId("qa-ask-sheet-name-row");

    // "Show my name"
    public ILocator AskSheetNameSwitch => page.GetByTestId("qa-ask-sheet-name-switch");

    // Off: "Off: nobody sees who asked, not even <presenter>."; on: "The room and <presenter> see this name with your
    // question."
    public ILocator AskSheetNameHint => page.GetByTestId("qa-ask-sheet-name-hint");

    // "Your name"
    public ILocator AskSheetName => page.GetByTestId("qa-ask-sheet-name");

    // "Send anonymously" or "Send as <name>"
    public ILocator AskSheetSend => page.GetByTestId("qa-ask-sheet-send");

    // While the question is on its way
    public ILocator AskSheetSending => page.GetByTestId("qa-ask-sheet-sending");

    // "You can't edit a question after sending it."
    public ILocator AskSheetFinePrint => page.GetByTestId("qa-ask-sheet-fine-print");

    // "Not sent yet. Your question is kept on this phone and sends by itself when the connection is back."
    public ILocator AskSheetQueued => page.GetByTestId("qa-ask-sheet-queued");

    // "You can ask again in 42 s"
    public ILocator AskSheetLimited => page.GetByTestId("qa-ask-sheet-limited");

    // A failed send's message; the draft stays
    public ILocator AskSheetFailed => page.GetByTestId("qa-ask-sheet-failed");

    // The banner in the ask sheet when a poll opens while it is open (qa/guest/PollOpenedBanner).
    public ILocator PollOpened => page.GetByTestId("qa-poll-opened");

    // "A poll just opened. It is on the Slide tab."
    public ILocator PollOpenedMessage => page.GetByTestId("qa-poll-opened-message");

    // "Go to poll"
    public ILocator PollOpenedGoToPoll => page.GetByTestId("qa-poll-opened-go");
}
