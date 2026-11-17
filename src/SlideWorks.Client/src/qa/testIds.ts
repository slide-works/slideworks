import type { QaAction, ReactionEmoji } from './types';

// Every SW-9 Q&A and reaction test id: the guest's tabs, list, ask sheet and reaction row (qa/guest), the qa slide and
// the layers on the big screen (slides/layouts/QaLayout, qa/screen), the presenter window's Questions panel, its
// settings strip, the deck page's card and the phone remote's strip (qa/presenter), taken from the mockup's phone,
// screen and controls states (design/sw-9-qa-reactions). Frozen after wave 0.1, so it holds the ids for every later
// wave. The acceptance page objects mirror them: Pages/GuestQuestions.cs, QaSlide.cs, QaPanel.cs, QaStrip.cs and
// QaSettingsCard.cs. Per-question ids take the question's number (1-based, as on the wire); per-reaction ids take the
// wire name; per-action ids take the action's route name (QA_ACTIONS). A comment gives the UI text, where there is one.

// The tab bar at the bottom of the guest page (qa/guest/GuestTabs). Absent when room.questions is off.
export const QA_TABS_TEST_IDS = {
  bar: 'qa-tabs',
  // "Slide"
  slideTab: 'qa-tab-slide',
  // The green dot on the Slide tab while a poll is open and Questions is showing: "A poll is open"
  slideDot: 'qa-tab-slide-dot',
  // "Questions", named "Questions, 6 open"
  questionsTab: 'qa-tab-questions',
  // The count of open questions on the Questions tab: "6"
  questionsBadge: 'qa-tab-questions-badge',
  // The tab panels
  slidePanel: 'qa-tab-panel-slide',
  questionsPanel: 'qa-tab-panel-questions',
} as const;

// The reaction row under the slide on the Slide tab (qa/guest/ReactionRow). Absent when reactions are off.
export const QA_REACTION_ROW_TEST_IDS = {
  // The group named "Reactions", greyed while cooling
  row: 'qa-reaction-row',
  // A failed tap's message
  error: 'qa-reaction-row-error',
  // The emoji that pops up from a tapped button; absent with reduced motion
  pop: 'qa-reaction-pop',
} as const;

// One reaction button, named "React thumbs up", showing its emoji.
export const reactionButtonTestId = (emoji: ReactionEmoji) => `qa-reaction-${emoji}`;

// The Questions tab's list (qa/guest/QuestionList, QuestionsEmpty, AskBar).
export const QA_LIST_TEST_IDS = {
  list: 'qa-list',
  // "Questions"
  heading: 'qa-list-heading',
  // "6 open"
  openCount: 'qa-list-open-count',
  // The sort's group, with "Top" and "New"
  sort: 'qa-list-sort',
  sortTop: 'qa-list-sort-top',
  sortNew: 'qa-list-sort-new',
  // The open questions, and the guest's own waiting and hidden ones pinned above them
  rows: 'qa-list-rows',
  // "Answered · 2"
  answered: 'qa-list-answered',
  answeredHeading: 'qa-list-answered-heading',
  // While the first read is on its way
  loading: 'qa-list-loading',
  // "Couldn't load the questions."
  failed: 'qa-list-failed',
  // "Try again"
  failedRetry: 'qa-list-failed-retry',
  // A failed upvote: the hook's error under the list
  error: 'qa-list-error',
  // QuestionsEmpty: "No questions yet" / "Ask the first one. Other people can upvote it, and <presenter> sees which
  // questions the room wants answered."
  empty: 'qa-list-empty',
  emptyTitle: 'qa-list-empty-title',
  emptyText: 'qa-list-empty-text',
  // "<presenter> has closed questions. You can still upvote."
  closed: 'qa-list-closed',
  // "Questions are off on this slide. You can still upvote."
  offHere: 'qa-list-off-here',
  // AskBar, which stays in view under the list
  askBar: 'qa-ask-bar',
  // "Ask a question"
  ask: 'qa-ask-bar-ask',
} as const;

// One question in the guest's list (qa/guest/QuestionRow).
export const questionRowTestId = (number: number) => `qa-question-${number}`;
export const questionTextTestId = (number: number) => `qa-question-text-${number}`;
// "Priya · 12 min", "Anonymous · now"
export const questionMetaTestId = (number: number) => `qa-question-meta-${number}`;
// The upvote toggle, named "Upvote, 14 votes: How do you split a big change?", with aria-pressed
export const questionUpvoteTestId = (number: number) => `qa-question-upvote-${number}`;
// The vote count beside the upvote, or on an answered question "… · 6 votes"
export const questionVotesTestId = (number: number) => `qa-question-votes-${number}`;
// In place of the upvote on the guest's own waiting ("…") or hidden ("–") question
export const questionNoVoteTestId = (number: number) => `qa-question-no-vote-${number}`;
// An answered question's "✓"
export const questionAnsweredTestId = (number: number) => `qa-question-answered-${number}`;
// Tags: "You", "Waiting for <presenter>", "On screen now", "Not shown to the room"
export const questionYouTestId = (number: number) => `qa-question-you-${number}`;
export const questionWaitingTestId = (number: number) => `qa-question-waiting-${number}`;
export const questionOnScreenTestId = (number: number) => `qa-question-on-screen-${number}`;
export const questionHiddenTestId = (number: number) => `qa-question-hidden-${number}`;

// The ask sheet over the list (qa/guest/AskSheet).
export const QA_ASK_SHEET_TEST_IDS = {
  sheet: 'qa-ask-sheet',
  // "Ask a question"
  title: 'qa-ask-sheet-title',
  // "✕"
  close: 'qa-ask-sheet-close',
  // The one-line text box: "What would you like <presenter> to answer?"
  text: 'qa-ask-sheet-text',
  // "One question at a time works best"
  hint: 'qa-ask-sheet-hint',
  // "42 / 200"
  counter: 'qa-ask-sheet-counter',
  // The name row, absent when names are off
  nameRow: 'qa-ask-sheet-name-row',
  // "Show my name"
  nameSwitch: 'qa-ask-sheet-name-switch',
  // Off: "Off: nobody sees who asked, not even <presenter>."; on: "The room and <presenter> see this name with your
  // question."
  nameHint: 'qa-ask-sheet-name-hint',
  // "Your name"
  name: 'qa-ask-sheet-name',
  // "Send anonymously" or "Send as <name>"
  send: 'qa-ask-sheet-send',
  // While the question is on its way
  sending: 'qa-ask-sheet-sending',
  // "You can't edit a question after sending it."
  finePrint: 'qa-ask-sheet-fine-print',
  // "Not sent yet. Your question is kept on this phone and sends by itself when the connection is back."
  queued: 'qa-ask-sheet-queued',
  // "You can ask again in 42 s"
  limited: 'qa-ask-sheet-limited',
  // A failed send's message; the draft stays
  failed: 'qa-ask-sheet-failed',
} as const;

// The banner in the ask sheet when a poll opens while it is open (qa/guest/PollOpenedBanner, D7).
export const QA_POLL_OPENED_TEST_IDS = {
  banner: 'qa-poll-opened',
  // "A poll just opened. It is on the Slide tab."
  message: 'qa-poll-opened-message',
  // "Go to poll"
  goToPoll: 'qa-poll-opened-go',
} as const;

// The qa slide on the big screen, the player, the presenter window and thumbnails (slides/layouts/QaLayout).
export const QA_SLIDE_TEST_IDS = {
  slide: 'qa-slide',
  // "Q&A"
  kicker: 'qa-slide-kicker',
  // The slot's title, or "Questions from the room"
  title: 'qa-slide-title',
  // The join pill, top right, while the deck is live
  join: 'qa-slide-join',
  // The top five open questions
  rows: 'qa-slide-rows',
  footer: 'qa-slide-footer',
  // "Open" or "Closed"
  phase: 'qa-slide-phase',
  // "6 questions"
  total: 'qa-slide-total',
  // "Top 5 of 7, by upvotes" or "Ask and upvote on your phone"
  footerNote: 'qa-slide-footer-note',
  // One question up, at full size, in place of the list
  oneUp: 'qa-slide-one-up',
  oneUpText: 'qa-slide-one-up-text',
  oneUpVotes: 'qa-slide-one-up-votes',
  // The name or "Anonymous"
  oneUpName: 'qa-slide-one-up-name',
  // "On screen now · 4 more waiting"
  oneUpMore: 'qa-slide-one-up-more',
  // "No questions yet" / "Ask on your phone. The most upvoted questions show here."
  empty: 'qa-slide-empty',
  // Closed and empty: "Questions are closed" / "Thanks for asking."
  closed: 'qa-slide-closed',
  // "Questions from the room appear here" / "While the deck is live, the audience asks and upvotes on their phones."
  notLive: 'qa-slide-not-live',
  // "Not live"
  notLivePill: 'qa-slide-not-live-pill',
} as const;

// One row of the qa slide's list, by question number: the row, its text (one line, cut with an ellipsis) and its votes.
export const qaSlideRowTestId = (number: number) => `qa-slide-row-${number}`;
export const qaSlideRowTextTestId = (number: number) => `qa-slide-row-text-${number}`;
export const qaSlideRowVotesTestId = (number: number) => `qa-slide-row-votes-${number}`;

// The question card over any slide (qa/screen/QuestionCardLayer).
export const QA_CARD_TEST_IDS = {
  layer: 'qa-card-layer',
  card: 'qa-card',
  // "Question from the room"
  label: 'qa-card-label',
  // "▲ 9"
  votes: 'qa-card-votes',
  // The name or "Anonymous"
  name: 'qa-card-name',
  text: 'qa-card-text',
} as const;

// The floating reactions up the right edge (qa/screen/ReactionsLayer).
export const QA_REACTIONS_LAYER_TEST_IDS = {
  layer: 'qa-reactions-layer',
  // One floating emoji; at most 30 on screen at once
  float: 'qa-reactions-float',
  // "+14 👏", the rest of a batch past 30, and the only thing drawn with reduced motion
  chip: 'qa-reactions-chip',
} as const;

// The Questions panel in the presenter window (qa/presenter/QaPanel, QaPanelTabs, ReactionCounter).
export const QA_PANEL_TEST_IDS = {
  panel: 'qa-panel',
  tabs: 'qa-panel-tabs',
  // "Open 3"
  tabOpen: 'qa-panel-tab-open',
  // "To review 1", shown only when review is on or something is waiting
  tabReview: 'qa-panel-tab-review',
  // The mark on "To review" while it isn't empty
  tabReviewMark: 'qa-panel-tab-review-mark',
  // "Answered 1"
  tabAnswered: 'qa-panel-tab-answered',
  // "Hidden 1"
  tabHidden: 'qa-panel-tab-hidden',
  // "Top ▾" / "Newest ▾", and its two options when it opens as a menu: "Top" and "Newest"
  sort: 'qa-panel-sort',
  sortTop: 'qa-panel-sort-top',
  sortNewest: 'qa-panel-sort-newest',
  rows: 'qa-panel-rows',
  // "No open questions. They appear here as the room asks."
  emptyOpen: 'qa-panel-empty-open',
  // "Questions are closed. Nothing new can be asked."
  closed: 'qa-panel-closed',
  // "Nothing waiting for approval."
  emptyReview: 'qa-panel-empty-review',
  // "Nothing answered yet."
  emptyAnswered: 'qa-panel-empty-answered',
  // "Nothing hidden."
  emptyHidden: 'qa-panel-empty-hidden',
  // "Questions start when you go live."
  notLive: 'qa-panel-not-live',
  // A refused call's message
  error: 'qa-panel-error',
  // The reactions counter: "👍 3  ❤️ 1"
  counter: 'qa-panel-counter',
  // "No reactions yet"
  counterEmpty: 'qa-panel-counter-empty',
  // "Reactions off"
  counterOff: 'qa-panel-counter-off',
} as const;

// One emoji's total in the panel's counter.
export const qaPanelReactionCountTestId = (emoji: ReactionEmoji) => `qa-panel-count-${emoji}`;

// One question in the panel, by number: the row, its text, its meta ("Priya · 12 min · On screen now") and its votes.
export const qaPanelRowTestId = (number: number) => `qa-panel-row-${number}`;
export const qaPanelRowTextTestId = (number: number) => `qa-panel-row-text-${number}`;
export const qaPanelRowMetaTestId = (number: number) => `qa-panel-row-meta-${number}`;
export const qaPanelRowVotesTestId = (number: number) => `qa-panel-row-votes-${number}`;
// A row's button for one action: "Approve", "Hide", "Restore", "Answered", "Reopen", "Show on screen" (or "Show on Q&A
// slide"), "Take off screen"; each named with its question ("Hide: How do you split a big change?").
export const qaPanelActionTestId = (action: QaAction, number: number) =>
  `qa-panel-${action}-${number}`;

// The settings strip in the Questions panel (qa/presenter/QaSettingsStrip): the deck's four toggles and the switch of
// the slide the presenter is on.
export const QA_SETTINGS_STRIP_TEST_IDS = {
  strip: 'qa-settings-strip',
  // "Questions open|closed"
  questions: 'qa-settings-strip-questions',
  // "Review first|off"
  review: 'qa-settings-strip-review',
  // "Names allowed|off"
  names: 'qa-settings-strip-names',
  // "Reactions on|off"
  reactions: 'qa-settings-strip-reactions',
  // This slide's switch: questions and reactions, each with its reason when the deck has that feature off
  slideQuestions: 'qa-settings-strip-slide-questions',
  slideQuestionsReason: 'qa-settings-strip-slide-questions-reason',
  slideReactions: 'qa-settings-strip-slide-reactions',
  slideReactionsReason: 'qa-settings-strip-slide-reactions-reason',
  // A failed change's message; the control goes back
  error: 'qa-settings-strip-error',
} as const;

// The deck page's "Q&A and reactions" card (qa/presenter/QaSettingsCard, QaSlideSwitch), for a person who can present.
export const QA_SETTINGS_CARD_TEST_IDS = {
  card: 'qa-settings-card',
  // "Q&A and reactions"
  title: 'qa-settings-card-title',
  // "Questions": Open, Closed, Off
  questions: 'qa-settings-card-questions',
  questionsOpen: 'qa-settings-card-questions-open',
  questionsClosed: 'qa-settings-card-questions-closed',
  questionsOff: 'qa-settings-card-questions-off',
  // "Review questions first"
  review: 'qa-settings-card-review',
  // "Names allowed"
  names: 'qa-settings-card-names',
  // "Reactions"
  reactions: 'qa-settings-card-reactions',
  // "Shown questions appear": "over any slide" / "only on the Q&A slide"
  showOn: 'qa-settings-card-show-on',
  showOnAnySlide: 'qa-settings-card-show-on-any-slide',
  showOnQaSlide: 'qa-settings-card-show-on-qa-slide',
  // "Slide 3: questions", and its reason when the deck has questions off
  slideQuestions: 'qa-settings-card-slide-questions',
  slideQuestionsReason: 'qa-settings-card-slide-questions-reason',
  // "Slide 3: reactions", and its reason when the deck has reactions off
  slideReactions: 'qa-settings-card-slide-reactions',
  slideReactionsReason: 'qa-settings-card-slide-reactions-reason',
  // A failed change's message; the control goes back
  error: 'qa-settings-card-error',
} as const;

// The questions strip on the presenter's phone remote (qa/presenter/QaStrip, QaStripSheet).
export const QA_STRIP_TEST_IDS = {
  strip: 'qa-strip',
  // "On screen", when the strip's question is on screen
  onScreen: 'qa-strip-on-screen',
  // "14"
  votes: 'qa-strip-votes',
  // "votes · 3 open · 1 to review"
  counts: 'qa-strip-counts',
  // The question's text, two lines at most
  text: 'qa-strip-text',
  // "No open questions"
  empty: 'qa-strip-empty',
  // "Show", on a question that isn't on screen
  show: 'qa-strip-show',
  // "Take off", on the question on screen
  takeOff: 'qa-strip-take-off',
  // "Answered"
  answer: 'qa-strip-answer',
  // "All 6", opens the sheet
  all: 'qa-strip-all',
  // A refused action's message
  error: 'qa-strip-error',
  // The sheet with the open and to-review questions
  sheet: 'qa-strip-sheet',
  sheetClose: 'qa-strip-sheet-close',
  sheetRows: 'qa-strip-sheet-rows',
  // A refused action's message in the sheet
  sheetError: 'qa-strip-sheet-error',
} as const;

// One question in the remote's sheet, by number, and its buttons: Approve, Hide, Show, Answered (as the panel's rows).
export const qaStripSheetRowTestId = (number: number) => `qa-strip-sheet-row-${number}`;
export const qaStripSheetActionTestId = (action: QaAction, number: number) =>
  `qa-strip-sheet-${action}-${number}`;
