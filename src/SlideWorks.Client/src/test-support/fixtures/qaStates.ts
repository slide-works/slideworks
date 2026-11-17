// SW-9's Q&A contract samples (Q1, Q4 to Q7, Q10) as typed fixtures, built from the JSON in src/contracts. Each call
// returns a fresh copy, so a test may change its own. qaStates.test.ts checks the exact key sets and the literal values.
import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestQuestionsJson from '@contracts/screens/guest-questions.json' with { type: 'json' };
import qaControlJson from '@contracts/screens/qa-control.json' with { type: 'json' };
import qaRoomJson from '@contracts/screens/qa-room.json' with { type: 'json' };
import questionAskJson from '@contracts/screens/question-ask.json' with { type: 'json' };
import questionVotesJson from '@contracts/screens/question-votes.json' with { type: 'json' };
import questionsReportJson from '@contracts/screens/questions-report.json' with { type: 'json' };
import type {
  AskedQuestion,
  AskRequest,
  GuestQuestion,
  GuestQuestionsView,
  MyVotes,
  QaControlView,
  QaError,
  QaRefusal,
  QaRoom,
  QaSettingsPatch,
  QaSlidePatch,
  QuestionControl,
  QuestionsReport,
  QuestionTally,
  ReactionRequest,
  ReactionsBurst,
  StaleVotes,
  TooManyQuestions,
  VoteRequest,
} from '../../qa/types';
import type { Widen } from './widen';

// qa-room.json: "open", "closed", "offHere" (the slide's switch is off; reactions off too) and "off" (no round,
// listRevision 0).
export type QaRoomSample = keyof typeof qaRoomJson;
// guest-questions.json: "view" (the guest's own question 4 is waiting) and "ownHidden" (the same, hidden by the
// presenter). The file also holds "guestErrors" and the reaction route's bodies under "reaction".
export type GuestQuestionsSample = 'view' | 'ownHidden';
// qa-control.json: "live" (review on, question 2 on screen, question 5 hidden, slide 3's questions off) and "notLive".
export type QaControlSample = 'live' | 'notLive';

const ROOMS = qaRoomJson satisfies Record<QaRoomSample, Widen<QaRoom>>;
const GUEST_QUESTIONS = guestQuestionsJson satisfies Record<
  GuestQuestionsSample,
  Widen<GuestQuestionsView>
> & {
  guestErrors: Widen<QaError>[];
  reaction: {
    request: Widen<ReactionRequest>;
    invalid: Widen<QaError>;
    reactionsOff: Widen<QaError>;
    tooManyReactions: Widen<QaError>;
  };
};
const ASK = questionAskJson satisfies {
  request: Widen<AskRequest>;
  created: Widen<AskedQuestion>;
  invalid: Widen<QaError>[];
  refusals: Widen<QaError>[];
  tooManyQuestions: Widen<TooManyQuestions>;
};
const VOTES = questionVotesJson satisfies {
  request: Widen<VoteRequest>;
  accepted: Widen<MyVotes>;
  stale: Widen<StaleVotes>;
  invalid: Widen<QaError>[];
  refusals: Widen<QaError>[];
  tooManyVotes: Widen<QaError>;
};
const CONTROLS = qaControlJson satisfies Record<QaControlSample, Widen<QaControlView>> & {
  settingsRequest: Widen<Required<QaSettingsPatch>>;
  slideRequest: Widen<Required<QaSlidePatch>>;
  refusals: Widen<QaRefusal>[];
};
const REPORT = questionsReportJson satisfies Widen<QuestionsReport>;
const EVENTS = audienceEventsJson satisfies {
  questionTally: Widen<QuestionTally>;
  questionControl: Widen<QuestionControl>;
  reactions: Widen<ReactionsBurst>;
};

const copy = <T>(value: unknown) => structuredClone(value) as T;

// audienceState.room and GuestQuestionsView.room.
export function qaRoom(sample: QaRoomSample = 'open', overrides: Partial<QaRoom> = {}): QaRoom {
  return { ...copy<QaRoom>(ROOMS[sample]), ...overrides };
}

// GET /api/join/{code}/questions.
export function guestQuestions(
  sample: GuestQuestionsSample = 'view',
  overrides: Partial<GuestQuestionsView> = {},
): GuestQuestionsView {
  return { ...copy<GuestQuestionsView>(GUEST_QUESTIONS[sample]), ...overrides };
}

// One question of the "view" sample, by number (1, 2, 3, 4 or 6), with any field changed.
export function guestQuestion(
  number: number,
  overrides: Partial<GuestQuestion> = {},
): GuestQuestion {
  const question = guestQuestions('view').questions.find((q) => q.number === number);
  if (!question) throw new Error(`guest-questions.json has no question ${number}`);
  return { ...question, ...overrides };
}

// The guest routes' shared error bodies: no_guest, no_session, too_many_attempts.
export function guestQaErrors(): QaError[] {
  return copy<QaError[]>(GUEST_QUESTIONS.guestErrors);
}

// POST /api/join/{code}/questions: the body, the 201 and the error bodies.
export function askRequest(overrides: Partial<AskRequest> = {}): AskRequest {
  return { ...copy<AskRequest>(ASK.request), ...overrides };
}

export function askedQuestion(): AskedQuestion {
  return copy<AskedQuestion>(ASK.created);
}

// The 400 bodies, in the order of Q4: text required, text at most 200, name at most 30, name not allowed.
export function askInvalid(): QaError[] {
  return copy<QaError[]>(ASK.invalid);
}

// The 409 bodies, one per code of ASK_REFUSALS.
export function askRefusals(): QaError[] {
  return copy<QaError[]>(ASK.refusals);
}

export function tooManyQuestions(overrides: Partial<TooManyQuestions> = {}): TooManyQuestions {
  return { ...copy<TooManyQuestions>(ASK.tooManyQuestions), ...overrides };
}

// PUT /api/join/{code}/questions/votes: the body, the 200, the 409 and the error bodies.
export function voteRequest(overrides: Partial<VoteRequest> = {}): VoteRequest {
  return { ...copy<VoteRequest>(VOTES.request), ...overrides };
}

export function votesAccepted(): MyVotes {
  return copy<MyVotes>(VOTES.accepted);
}

export function votesStale(): StaleVotes {
  return copy<StaleVotes>(VOTES.stale);
}

// The 400 bodies: unknown question 7, duplicate.
export function votesInvalid(): QaError[] {
  return copy<QaError[]>(VOTES.invalid);
}

// The 409 body with questions off.
export function votesRefusals(): QaError[] {
  return copy<QaError[]>(VOTES.refusals);
}

export function tooManyVotes(): QaError {
  return copy<QaError>(VOTES.tooManyVotes);
}

// POST /api/join/{code}/reactions: the body and the error bodies (the 204 has none).
export function reactionRequest(overrides: Partial<ReactionRequest> = {}): ReactionRequest {
  return { ...copy<ReactionRequest>(GUEST_QUESTIONS.reaction.request), ...overrides };
}

export function reactionInvalid(): QaError {
  return copy<QaError>(GUEST_QUESTIONS.reaction.invalid);
}

export function reactionsOff(): QaError {
  return copy<QaError>(GUEST_QUESTIONS.reaction.reactionsOff);
}

export function tooManyReactions(): QaError {
  return copy<QaError>(GUEST_QUESTIONS.reaction.tooManyReactions);
}

// The presenter's view, live or not.
export function qaControlView(
  sample: QaControlSample = 'live',
  overrides: Partial<QaControlView> = {},
): QaControlView {
  return { ...copy<QaControlView>(CONTROLS[sample]), ...overrides };
}

// PUT /api/decks/{id}/qa/settings and PUT /api/decks/{id}/qa/slides/{slide}, each with every field given.
export function qaSettingsRequest(): Required<QaSettingsPatch> {
  return copy<Required<QaSettingsPatch>>(CONTROLS.settingsRequest);
}

export function qaSlideRequest(): Required<QaSlidePatch> {
  return copy<Required<QaSlidePatch>>(CONTROLS.slideRequest);
}

// The 409 bodies of the presenter routes, one per code of QA_REFUSALS.
export function qaRefusals(): QaRefusal[] {
  return copy<QaRefusal[]>(CONTROLS.refusals);
}

// get_questions.
export function questionsReport(): QuestionsReport {
  return copy<QuestionsReport>(REPORT);
}

// The three events as they arrive on "audienceEvent".
export function questionTally(overrides: Partial<QuestionTally> = {}): QuestionTally {
  return { ...copy<QuestionTally>(EVENTS.questionTally), ...overrides };
}

export function questionControl(control?: QaControlView): QuestionControl {
  const event = copy<QuestionControl>(EVENTS.questionControl);
  return control === undefined ? event : { ...event, control };
}

export function reactionsBurst(overrides: Partial<ReactionsBurst> = {}): ReactionsBurst {
  return { ...copy<ReactionsBurst>(EVENTS.reactions), ...overrides };
}
