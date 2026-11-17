// Every SW-6 poll test id: the guest's poll card, the poll slide, the presenter window's panel and the phone remote's
// strip, taken from the mockup's states (design/sw-6-polls). Frozen after wave 0, so it holds the ids for every later
// wave. The acceptance page objects mirror them: Pages/GuestPollCard.cs, PollSlide.cs, PollPanel.cs and PollStrip.cs.
// Per-option ids take the option's 0-based position, the same index as `picks`. Per-round ids take the round's number.

// The poll card on a guest's phone (polls/guest). Each part's root id is the stub's root: PollOptions, PollOptionsMany,
// PollFoot, PollVerdict, PollConflict and PollQueued.
export const POLL_CARD_TEST_IDS = {
  card: 'poll-card',
  // "Poll · Pick one", "Poll · Pick up to 2"
  kind: 'poll-card-kind',
  // "Round 2", shown from round 2 on
  round: 'poll-card-round',
  question: 'poll-card-question',
  // The footer under the card: "Private: <presenter> sees totals only"
  privacy: 'poll-card-privacy',

  // PollOptions: the radio group for pick one
  options: 'poll-options',
  // PollOptionsMany: the checkbox group for pick several
  optionsMany: 'poll-options-many',

  // PollFoot: what the card says under the options
  foot: 'poll-foot',
  // "Tap an answer to send it."
  tapHint: 'poll-foot-tap-hint',
  // "1 of 2 picked"
  pickCount: 'poll-foot-pick-count',
  send: 'poll-foot-send',
  sending: 'poll-foot-sending',
  // "Answer sent"
  sent: 'poll-foot-sent',
  // "38 answers so far", a polite live region
  answered: 'poll-foot-answered',
  // "Results show here and on the big screen when <presenter> closes the poll."
  resultsLater: 'poll-foot-results-later',
  // "Tap another option to change it"
  changeHint: 'poll-foot-change-hint',
  // Pick several, after sending: "Change my answer"
  change: 'poll-foot-change',
  // "Poll closed", with the final count
  closed: 'poll-foot-closed',
  // "You didn't send an answer for this one."
  noAnswer: 'poll-foot-no-answer',
  // "You joined after this poll closed."
  joinedLate: 'poll-foot-joined-late',
  // A manual poll before it opens: "<presenter> hasn't opened this poll yet."
  notOpen: 'poll-foot-not-open',
  // A failed call's message. Nothing fails silently.
  failed: 'poll-foot-failed',

  // PollVerdict: after the presenter shows the answer
  verdict: 'poll-verdict',
  // "You got it."
  verdictRight: 'poll-verdict-right',
  // "Not this time. The answer was …"
  verdictMiss: 'poll-verdict-miss',

  // PollConflict: a 409 came back; the pick is kept and the card asks again. Focus moves to the message.
  conflict: 'poll-conflict',
  conflictMessage: 'poll-conflict-message',
  conflictSend: 'poll-conflict-send',
  conflictClear: 'poll-conflict-clear',

  // PollQueued: offline or reconnecting with an unsent pick. "Not sent yet"
  queued: 'poll-queued',
  queuedMessage: 'poll-queued-message',
} as const;

// One option on the card: the radio or checkbox itself.
export const pollOptionTestId = (position: number) => `poll-option-${position}`;
// Its bar, its percentage, its "You" mark and its correct-answer mark, each present only when shown.
export const pollOptionBarTestId = (position: number) => `poll-option-bar-${position}`;
export const pollOptionPercentTestId = (position: number) => `poll-option-percent-${position}`;
export const pollOptionYouTestId = (position: number) => `poll-option-you-${position}`;
export const pollOptionCorrectTestId = (position: number) => `poll-option-correct-${position}`;

// The poll slide on the big screen, the player, the presenter window and thumbnails (slides/layouts/PollLayout).
export const POLL_SLIDE_TEST_IDS = {
  slide: 'poll-slide',
  // The join pill, top right, while the deck is live
  join: 'poll-slide-join',
  // "Poll · Pick one"
  kind: 'poll-slide-kind',
  question: 'poll-slide-question',
  rows: 'poll-slide-rows',
  // "Not open yet", "Open" or "Closed"
  state: 'poll-slide-state',
  // "38 answers"
  answered: 'poll-slide-answered',
  // "Results show when the poll closes", while the room can't see the counts
  hiddenNote: 'poll-slide-hidden-note',
  // "Share of people who picked each. Adds up to more than 100%."
  manyNote: 'poll-slide-many-note',
  // "Round 2"
  round: 'poll-slide-round',
} as const;

export const pollSlideRowTestId = (position: number) => `poll-slide-row-${position}`;
export const pollSlideBarTestId = (position: number) => `poll-slide-bar-${position}`;
export const pollSlidePercentTestId = (position: number) => `poll-slide-percent-${position}`;
export const pollSlideCorrectTestId = (position: number) => `poll-slide-correct-${position}`;

// The poll panel in the presenter window (polls/presenter/PollPanel).
export const POLL_PANEL_TEST_IDS = {
  panel: 'poll-panel',
  // "No poll on this slide."
  empty: 'poll-panel-empty',
  // "Draft", "Open" or "Closed"
  state: 'poll-panel-state',
  // "round 2"
  round: 'poll-panel-round',
  // "Pick one · Live"
  settings: 'poll-panel-settings',
  // The answer count
  answered: 'poll-panel-answered',
  // "93% of 41 joined"
  rate: 'poll-panel-rate',
  bars: 'poll-panel-bars',
  // What the room sees: "The room sees the bars" or "Hidden from the room until …"
  room: 'poll-panel-room',
  open: 'poll-panel-open',
  close: 'poll-panel-close',
  reveal: 'poll-panel-reveal',
  showAnswer: 'poll-panel-show-answer',
  newRound: 'poll-panel-new-round',
  // Earlier rounds of this session: "Round 1: 38 answers, kept"
  history: 'poll-panel-history',
  // A refused call's message
  error: 'poll-panel-error',
  // "P open or close the poll · R reveal · A show answer"
  keys: 'poll-panel-keys',
} as const;

export const pollPanelRowTestId = (position: number) => `poll-panel-row-${position}`;
export const pollPanelBarTestId = (position: number) => `poll-panel-bar-${position}`;
export const pollPanelCountTestId = (position: number) => `poll-panel-count-${position}`;
// The correct row's mark once the answer is shown. The mockup marks it on the row itself, not apart from the rows.
export const pollPanelCorrectTestId = (position: number) => `poll-panel-correct-${position}`;
export const pollPanelHistoryRoundTestId = (round: number) => `poll-panel-history-round-${round}`;

// The poll strip on the presenter's phone remote (polls/presenter/PollStrip).
export const POLL_STRIP_TEST_IDS = {
  strip: 'poll-strip',
  state: 'poll-strip-state',
  answered: 'poll-strip-answered',
  // "93% of 41"
  rate: 'poll-strip-rate',
  open: 'poll-strip-open',
  close: 'poll-strip-close',
  reveal: 'poll-strip-reveal',
  showAnswer: 'poll-strip-show-answer',
  newRound: 'poll-strip-new-round',
  error: 'poll-strip-error',
} as const;
