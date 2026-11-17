// SW-9's Q&A and reaction shapes (Q1 and Q4 to Q7, Q10), mirroring src/contracts/screens/qa-room.json,
// guest-questions.json, question-ask.json, question-votes.json, qa-control.json, questions-report.json and the
// questionTally, questionControl and reactions samples in audience-events.json, and the C# records in
// Features/Qa/Contracts. Frozen after wave 0.1: qaStates.test.ts round-trips each sample.
//
// A question is known by its `number`, its 1-based position in its round; it is the only id on the wire, and the
// guest's upvote picks are question numbers. `seq` and `listRevision` are plain integers that only increase (within a
// round for `listRevision`).

// The deck's questions setting: open, closed (the list and upvotes stay, nothing new can be asked) or off (no Questions
// tab and no round).
export type QaQuestions = 'open' | 'closed' | 'off';

// Where a shown question appears: over any slide, or only while a qa slide is up.
export type QaShowOn = 'anySlide' | 'qaSlide';

// The deck's five Q&A settings (Q1). A deck with no settings row has the defaults: open, false, true, true, anySlide.
export type QaSettings = {
  questions: QaQuestions;
  // True makes a new question wait for the presenter's approval.
  review: boolean;
  // False removes "Show my name" from the ask sheet and refuses a name.
  names: boolean;
  reactions: boolean;
  showOn: QaShowOn;
};

// PUT /api/decks/{id}/qa/settings: any of the five fields.
export type QaSettingsPatch = Partial<QaSettings>;

// PUT /api/decks/{id}/qa/slides/{slide}: a slide's switch, either field may be left out. A switch can only turn off what
// the deck has on.
export type QaSlidePatch = { questions?: boolean; reactions?: boolean };

// What the room may do with questions on the slide on screen: offHere means the slide's switch is off while the deck is
// open; closed wins over offHere.
export type QaRoomQuestions = 'open' | 'closed' | 'offHere' | 'off';

// audienceState.room and GuestQuestionsView.room (Q1): the effective values for the slide on screen, worked out only
// by the server's QaRoomRules. `round` is the Q&A round id, null when questions are off. `listRevision` is 0 whenever
// `round` is null; within a round it only increases. A phone re-reads its list when `round` or `listRevision` changes.
export type QaRoom = {
  round: string | null;
  questions: QaRoomQuestions;
  names: boolean;
  reactions: boolean;
  listRevision: number;
};

// A question's state as a guest or an agent sees it: open or answered, and for the guest's own question (and in
// get_questions) also waiting or hidden. Hidden wins over the state under it.
export type QuestionStatus = 'open' | 'answered' | 'waiting' | 'hidden';

// One question in a guest's list (Q4). `name` is null for an anonymous question. `age` is a label the server makes
// ("now", "2 min", "1 h"), so the phone never reads a clock. `yours` is true only on the asker's own question.
export type GuestQuestion = {
  number: number;
  text: string;
  name: string | null;
  age: string;
  state: QuestionStatus;
  votes: number;
  onScreen: boolean;
  yours: boolean;
};

// A guest's own upvotes in the round: question numbers, and the revision of its answer (0 before its first).
export type MyVotes = { picks: number[]; revision: number };

// GET /api/join/{code}/questions: every open and answered question that isn't hidden, plus this guest's own in any
// state, in `number` order. The phone sorts.
export type GuestQuestionsView = {
  room: QaRoom;
  questions: GuestQuestion[];
  mine: MyVotes;
};

// POST /api/join/{code}/questions. `name` is null for an anonymous question.
export type AskRequest = { text: string; name: string | null };

// Its 201: the new question (with the asker's own upvote, unless the writer refused it) and the guest's upvotes.
export type AskedQuestion = { question: GuestQuestion; mine: MyVotes };

// Why asking was refused (409 { error }). Nothing was written.
export const ASK_REFUSALS = ['questions_closed', 'questions_off', 'questions_off_here'] as const;

export type AskRefusalCode = (typeof ASK_REFUSALS)[number];

// A 400 names the rule ("text: required", "text: at most 200", "name: at most 30", "name: not allowed"), or is
// no_guest; 404 is no_session; a 409 is one of ASK_REFUSALS or questions_off; 429 is too_many_attempts or
// too_many_votes; 413 is a body over its size limit.
export type QaError = { error: string };

// Asking's 429: past 3 questions a minute per guest. `retryAfter` is whole seconds.
export type TooManyQuestions = { error: 'too_many_questions'; retryAfter: number };

// PUT /api/join/{code}/questions/votes. `picks` are question numbers; `revision` is the one the guest holds.
export type VoteRequest = { round: string; picks: number[]; revision: number };

// The vote's 409: the round isn't the open one or the revision doesn't match. Nothing was written, and `current` is
// what the phone should show now.
export type StaleVotes = { error: 'stale'; current: GuestQuestionsView };

// POST /api/join/{code}/reactions → 204. 400 "emoji: unknown", 409 reactions_off, 429 too_many_reactions.
export type ReactionEmoji = 'thumbsUp' | 'heart' | 'laugh' | 'wow' | 'clap';

export type ReactionRequest = { emoji: ReactionEmoji };

// One count per emoji, in the order of qa/emoji.ts.
export type ReactionCounts = Record<ReactionEmoji, number>;

// A question's stored state (D13). Hidden and on screen are flags beside it.
export type QuestionState = 'waiting' | 'open' | 'answered';

// One question in the presenter's view: every question of the round, in `number` order. The panel sorts.
export type QaQuestion = {
  number: number;
  text: string;
  name: string | null;
  age: string;
  state: QuestionState;
  hidden: boolean;
  onScreen: boolean;
  votes: number;
};

// A slide's switch, by 1-based slide number.
export type QaSlideView = { slide: number; questions: boolean; reactions: boolean };

// How many questions of the round are in each tab. A hidden question counts as hidden only.
export type QaCounts = { open: number; waiting: number; answered: number; hidden: number };

// The presenter's view (Q6): every presenter route's response and the questionControl payload. `seq` only increases,
// so a lower one is dropped. `here` is the switch of the presenter's current slide, null when not live. `slides` lists
// every slide with something switched off; a slide not in it has both on. `onScreen` is a question number or null.
// `reactions` is the session's totals per emoji. Not live: `live` false, `round` null, no questions, zero counts.
export type QaControlView = {
  seq: number;
  live: boolean;
  round: string | null;
  settings: QaSettings;
  here: QaSlideView | null;
  slides: QaSlideView[];
  counts: QaCounts;
  onScreen: number | null;
  questions: QaQuestion[];
  reactions: ReactionCounts;
};

// Why a presenter route refused (409 { error }). An unknown question number is 404. A refused call changes nothing.
export const QA_REFUSALS = ['not_live', 'wrong_state'] as const;

export type QaRefusalCode = (typeof QA_REFUSALS)[number];

export type QaRefusal = { error: QaRefusalCode };

// The seven moderation actions, each POST /api/decks/{id}/qa/questions/{number}/{action}.
export const QA_ACTIONS = [
  'approve',
  'hide',
  'restore',
  'answer',
  'reopen',
  'show',
  'unshow',
] as const;

export type QaAction = (typeof QA_ACTIONS)[number];

// get_questions over MCP (Q10). Never which guest asked or upvoted. Sessions newest first.
export type QuestionsReportQuestion = {
  text: string;
  name: string | null;
  state: QuestionStatus;
  votes: number;
  askedAt: string;
};

export type QuestionsReportSession = {
  startedAt: string;
  asked: number;
  answered: number;
  hidden: number;
  questions: QuestionsReportQuestion[];
};

export type QuestionsReport = { sessions: QuestionsReportSession[] };

// The three events (Q5), on the hub method "audienceEvent".

// To guests and the big screen: the votes of only the questions another guest may see. A phone re-reads its list
// when `listRevision` is higher than the one it holds; otherwise it applies the votes.
export type QuestionVotes = { number: number; votes: number };

export type QuestionTally = {
  type: 'questionTally';
  round: string;
  listRevision: number;
  votes: QuestionVotes[];
};

// To the presenter's screens only (audience-watch group).
export type QuestionControl = { type: 'questionControl'; control: QaControlView };

// To the presenter's screens only: the taps on the 1-based `slide` since the last batch. Never to phones.
export type ReactionsBurst = { type: 'reactions'; slide: number; counts: ReactionCounts };
