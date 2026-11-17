// SW-6's poll contract samples (V4, V5, V9) as typed fixtures, built from the JSON in src/contracts. Each call returns a
// fresh copy, so a test may change its own. pollStates.test.ts checks the exact key sets and the literal values.
import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestPollJson from '@contracts/screens/guest-poll.json' with { type: 'json' };
import pollActivityJson from '@contracts/screens/poll-activity.json' with { type: 'json' };
import pollAnswerJson from '@contracts/screens/poll-answer.json' with { type: 'json' };
import pollControlJson from '@contracts/screens/poll-control.json' with { type: 'json' };
import pollResultsJson from '@contracts/screens/poll-results.json' with { type: 'json' };
import type {
  AnswerError,
  AnswerRequest,
  GuestPollView,
  MyAnswer,
  PollActivity,
  PollControl,
  PollControlView,
  PollRefusal,
  PollResultsView,
  PollTally,
  StaleAnswer,
} from '../../polls/types';
import type { Widen } from './widen';

// poll-activity.json: "draft", "open", "closed" and "answerShown".
export type PollActivitySample = keyof typeof pollActivityJson;
// guest-poll.json: "answered" (closed with the answer shown), "hidden" (counts hidden after answering), "notAnswered"
// and "noPoll".
export type GuestPollSample = keyof typeof guestPollJson;
// poll-control.json: "draft", "open" and "closed" ("refusals" holds the 409 bodies).
export type PollControlSample = Exclude<keyof typeof pollControlJson, 'refusals'>;

const ACTIVITIES = pollActivityJson satisfies Record<PollActivitySample, Widen<PollActivity>>;
const GUEST_POLLS = guestPollJson satisfies Record<GuestPollSample, Widen<GuestPollView>>;
const ANSWERS = pollAnswerJson satisfies {
  request: Widen<AnswerRequest>;
  accepted: Widen<MyAnswer>;
  stale: Widen<StaleAnswer>;
  invalid: Widen<AnswerError>[];
  noGuest: Widen<AnswerError>;
  noSession: Widen<AnswerError>;
  tooManyAnswers: Widen<AnswerError>;
};
const CONTROLS = pollControlJson satisfies Record<PollControlSample, Widen<PollControlView>> & {
  refusals: Widen<PollRefusal>[];
};
const RESULTS = pollResultsJson satisfies Widen<PollResultsView>;
const EVENTS = audienceEventsJson satisfies {
  pollTally: Widen<PollTally>;
  pollControl: Widen<PollControl>;
};

const copy = <T>(value: unknown) => structuredClone(value) as T;

// audienceState.activity for a poll in each phase.
export function pollActivity(
  sample: PollActivitySample = 'open',
  overrides: Partial<PollActivity> = {},
): PollActivity {
  return { ...copy<PollActivity>(ACTIVITIES[sample]), ...overrides };
}

// GET /api/join/{code}/poll.
export function guestPoll(sample: GuestPollSample = 'answered'): GuestPollView {
  return copy<GuestPollView>(GUEST_POLLS[sample]);
}

// PUT /api/join/{code}/poll/answer: the body, the 200, the 409 and the error bodies.
export function answerRequest(overrides: Partial<AnswerRequest> = {}): AnswerRequest {
  return { ...copy<AnswerRequest>(ANSWERS.request), ...overrides };
}

export function answerAccepted(): MyAnswer {
  return copy<MyAnswer>(ANSWERS.accepted);
}

export function answerStale(): StaleAnswer {
  return copy<StaleAnswer>(ANSWERS.stale);
}

// The 400 bodies, in the order of V5: pick one, at most 2, unknown option 7, duplicate, required.
export function answerInvalid(): AnswerError[] {
  return copy<AnswerError[]>(ANSWERS.invalid);
}

export function answerNoGuest(): AnswerError {
  return copy<AnswerError>(ANSWERS.noGuest);
}

export function answerNoSession(): AnswerError {
  return copy<AnswerError>(ANSWERS.noSession);
}

export function answerTooMany(): AnswerError {
  return copy<AnswerError>(ANSWERS.tooManyAnswers);
}

// The presenter's view in each phase.
export function pollControlView(
  sample: PollControlSample = 'open',
  overrides: Partial<PollControlView> = {},
): PollControlView {
  return { ...copy<PollControlView>(CONTROLS[sample]), ...overrides };
}

// The 409 bodies of the poll routes, one per refusal code.
export function pollRefusals(): PollRefusal[] {
  return copy<PollRefusal[]>(CONTROLS.refusals);
}

// get_poll_results.
export function pollResults(): PollResultsView {
  return copy<PollResultsView>(RESULTS);
}

// The two events as they arrive on "audienceEvent".
export function pollTally(overrides: Partial<PollTally> = {}): PollTally {
  return { ...copy<PollTally>(EVENTS.pollTally), ...overrides };
}

export function pollControl(control?: PollControlView): PollControl {
  const event = copy<PollControl>(EVENTS.pollControl);
  return control === undefined ? event : { ...event, control };
}
