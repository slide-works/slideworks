// SW-6's poll shapes (V1, V4, V5, V9), mirroring src/contracts/screens/poll-activity.json, guest-poll.json,
// poll-answer.json, poll-control.json, poll-results.json and the pollTally and pollControl samples in
// audience-events.json, and the C# records in Features/Polls/Contracts. Frozen after wave 0: pollStates.test.ts
// round-trips each sample. Options are known by position: picks are 0-based, `correct` is 1-based.

// How many options a guest picks: one (a tap sends it) or up to `max` (tap to select, then Send).
export type PollPick = 'one' | 'upTo';

// When the room sees the counts: live, after a guest has answered (that guest only), or after the poll closes.
export type PollResults = 'live' | 'afterAnswer' | 'afterClose';

// When the first round opens: when the presenter reaches the slide, or only when they press Open.
export type PollOpens = 'onArrival' | 'manual';

// Who sees which option a guest picked: nobody, only totals. The only value in SW-6.
export type PollPrivacy = 'private';

// Where the poll's current round is in this audience session.
export type PollPhase = 'draft' | 'open' | 'closed';

// A poll slide's settings (V1). Never in the slots, and never sent to a guest whole.
export type PollSettings = {
  pick: PollPick;
  // 2 to the option count when pick is 'upTo', else null.
  max: number | null;
  results: PollResults;
  // The 1-based correct option, or null.
  correct: number | null;
  opens: PollOpens;
  privacy: PollPrivacy;
  // Left off a choice poll. text is a free answer shown as a list; cloud is a word cloud.
  answers?: 'choice' | 'text' | 'cloud';
};

// audienceState.activity for a poll slide (V5). `round` is null in draft; `number` is the round's number, or in draft
// the number it gets when opened. `correct` is null until the presenter shows the answer. Never counts, opens or
// privacy.
export type PollActivity = {
  type: 'poll';
  round: string | null;
  number: number;
  phase: PollPhase;
  question: string;
  options: string[];
  pick: PollPick;
  max: number | null;
  results: PollResults;
  roomSeesCounts: boolean;
  correct: number | null;
  // Left off a choice poll.
  answers?: 'choice' | 'text' | 'cloud';
};

// How many have answered and, when the receiver may see them, one count per option (null while hidden). A tally with
// null counts never clears counts the phone already holds for that round.
export type PollTally = {
  type: 'pollTally';
  round: string;
  answered: number;
  counts: number[] | null;
};

// A guest's own answer: 0-based picks, ascending, and its revision (1 for a first answer). Also the 200 body of PUT
// /api/join/{code}/poll/answer.
export type MyAnswer = { picks: number[]; revision: number };

// GET /api/join/{code}/poll. joinedLate is true only when the round is closed, this guest has no answer in it and joined
// the session after it closed ("You joined after this poll closed"); false otherwise.
export type GuestPollView = {
  activity: PollActivity | null;
  mine: MyAnswer | null;
  tally: PollTally | null;
  joinedLate: boolean;
};

// The body of PUT /api/join/{code}/poll/answer. `revision` is the answer the guest holds (0 for a first answer).
export type AnswerRequest = { round: string; picks: number[]; revision: number; text?: string };

// The 409 body: nothing was written, and `current` is what the phone should show now.
export type StaleAnswer = { error: 'stale'; current: GuestPollView };

// A 400 names the rule ("picks: at most 2"), or is no_guest; 404 is no_session; 429 is too_many_answers.
export type AnswerError = { error: string };

// One closed round of this session in the presenter's view.
export type PollRoundSummary = { number: number; answered: number; joined: number };

// The presenter's view of a poll slide (V4): every poll route's response and the pollControl payload. `seq` only
// increases, so a lower one is dropped. `slide` is 1-based. Counts are always there for a person who can present;
// `roomSeesCounts` says whether the room sees them too.
export type PollControlView = {
  seq: number;
  slide: number;
  round: string | null;
  number: number;
  phase: PollPhase;
  settings: PollSettings;
  question: string;
  options: string[];
  answered: number;
  joined: number;
  counts: number[];
  roomSeesCounts: boolean;
  revealed: boolean;
  answerShown: boolean;
  rounds: PollRoundSummary[];
  replies?: string[] | null;
  words?: { text: string; count: number }[] | null;
};

// Sent only to the presenter's screens (audience-watch group).
export type PollControl = { type: 'pollControl'; control: PollControlView };

// Why a poll route refused (409 { error }).
export const POLL_REFUSALS = [
  'not_live',
  'wrong_phase',
  'another_poll_open',
  'already_visible',
  'no_correct',
] as const;

export type PollRefusalCode = (typeof POLL_REFUSALS)[number];

export type PollRefusal = { error: PollRefusalCode };

// get_poll_results over MCP (V9). Totals only, never who picked what.
export type PollResultsRound = {
  number: number;
  // True for the round that is still open: its answered, joined, rate and counts are live and may change. A closed round's are final.
  open: boolean;
  // The option texts this round's counts belong to: a closed round's own, an open round's the slide's current ones.
  options: string[];
  answered: number;
  joined: number;
  // answered ÷ joined
  rate: number;
  // One count per entry of this round's options.
  counts: number[];
};

export type PollResultsSession = { startedAt: string; rounds: PollResultsRound[] };

export type PollResultsView = {
  question: string;
  options: string[];
  sessions: PollResultsSession[];
};
