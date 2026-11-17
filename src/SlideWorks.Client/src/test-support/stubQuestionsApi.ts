import type { Page, Route } from 'playwright/test';
import { REACTION_EMOJI } from '../qa/emoji';
import type {
  AskRequest,
  GuestQuestion,
  GuestQuestionsView,
  MyVotes,
  QaRoom,
  ReactionEmoji,
  ReactionRequest,
  VoteRequest,
} from '../qa/types';

// One scripted answer to a questions or reactions call. With status or json it answers with them (status 200 when left
// out), such as { status: 404, json: { error: 'no_session' } }; networkDown fails the call before it reaches the server;
// with neither it answers as the server would. hold keeps it unanswered until release().
export type StubQaReply = {
  status?: number;
  json?: unknown;
  networkDown?: boolean;
  hold?: boolean;
};

export interface StubQuestionsApiOptions {
  // The join code the page is on. '482913' when left out, the guest fixtures' code.
  code?: string;
  // The guest's view: what the GET answers and what asks, votes and taps are checked against. Its room says what the
  // room may do. An open room with no questions when left out.
  view?: GuestQuestionsView;
  // Whether a new question waits for approval (the deck's review setting, which the guest's room doesn't carry).
  review?: boolean;
  // The seconds a 429 too_many_questions says to wait.
  retryAfter?: number;
}

const ROUND = '6f1d2c3b-4a5e-4f60-8a7b-9c0d1e2f3a4b';

const OPEN_ROOM: QaRoom = {
  round: ROUND,
  questions: 'open',
  names: true,
  reactions: true,
  listRevision: 1,
};

// The limits of D20, counted over the stub's life: it has no clock, so a spec that needs them again mounts a new stub.
export const QUESTIONS_PER_MINUTE = 3;
export const VOTE_CHANGES_PER_MINUTE = 30;
export const TAPS_PER_FIVE_SECONDS = 10;

const ASK_REFUSAL = {
  closed: 'questions_closed',
  offHere: 'questions_off_here',
  off: 'questions_off',
} as const;

const MAX_TEXT = 200;
const MAX_NAME = 30;
const collapse = (value: string | null | undefined) =>
  (value ?? '').split(/\s+/).filter(Boolean).join(' ');

// Q4's rules for a question's text and name, in the server's order (QuestionText.Check), or null when they pass.
function askRule(request: AskRequest, names: boolean): string | null {
  const text = collapse(request.text);
  if (text.length === 0) return 'text: required';
  if (text.length > MAX_TEXT) return `text: at most ${MAX_TEXT}`;
  const name = collapse(request.name);
  if (name.length === 0) return null;
  if (!names) return 'name: not allowed';
  return name.length > MAX_NAME ? `name: at most ${MAX_NAME}` : null;
}

type Answer = { status: number; json?: unknown };

// Component tests only: plays the guest's question and reaction routes (Q4, Q7) at the network boundary for one code,
// holding that guest's GuestQuestionsView, and refuses by default what the server refuses:
// - GET /api/join/{code}/questions answers with the view.
// - POST …/questions: 400 { error } naming the first broken text or name rule; 409 questions_closed, questions_off or
//   questions_off_here from the room; 429 { error: 'too_many_questions', retryAfter } past 3 asks. Otherwise 201: the
//   question gets the next number, state waiting with review on or else open, the asker's upvote, and joins the view,
//   whose listRevision goes up.
// - PUT …/questions/votes: 409 questions_off with no round; 409 { error: 'stale', current } when the round isn't the
//   room's or the revision isn't the guest's; 400 "picks: duplicate" or "picks: unknown question n"; 429 too_many_votes
//   past 30 changes. Otherwise 200 { picks, revision + 1 }: picks on questions that can't be voted on now (answered,
//   waiting, hidden) stay as they were, the rest are applied and the votes follow.
// - POST /api/join/{code}/reactions: 400 "emoji: unknown"; 409 reactions_off from the room; 429 too_many_reactions past
//   10 taps. Otherwise 204.
// Nothing is written on a refusal. Any route can be scripted instead with the queue methods. Every call is recorded,
// held and failed ones included. Call it before mount().
export async function stubQuestionsApi(
  page: Page,
  { code = '482913', view, review = false, retryAfter = 60 }: StubQuestionsApiOptions = {},
) {
  let current: GuestQuestionsView = structuredClone(
    view ?? { room: OPEN_ROOM, questions: [], mine: { picks: [], revision: 0 } },
  );
  const queues = {
    get: [] as StubQaReply[],
    ask: [] as StubQaReply[],
    vote: [] as StubQaReply[],
    react: [] as StubQaReply[],
  };
  const held: (() => Promise<void>)[] = [];
  let gets = 0;
  let accepted = { asks: 0, voteChanges: 0, taps: 0 };
  const asks: AskRequest[] = [];
  const votes: VoteRequest[] = [];
  const taps: ReactionRequest[] = [];

  const bumpList = () => {
    current = {
      ...current,
      room: { ...current.room, listRevision: current.room.listRevision + 1 },
    };
  };

  const answerAsk = (request: AskRequest): Answer => {
    const rule = askRule(request, current.room.names);
    if (rule !== null) return { status: 400, json: { error: rule } };
    if (current.room.questions !== 'open')
      return { status: 409, json: { error: ASK_REFUSAL[current.room.questions] } };
    if (accepted.asks >= QUESTIONS_PER_MINUTE)
      return { status: 429, json: { error: 'too_many_questions', retryAfter } };
    accepted.asks += 1;
    const number = Math.max(0, ...current.questions.map((q) => q.number)) + 1;
    const name = collapse(request.name);
    const question: GuestQuestion = {
      number,
      text: collapse(request.text),
      name: name.length > 0 ? name : null,
      age: 'now',
      state: review ? 'waiting' : 'open',
      votes: 1,
      onScreen: false,
      yours: true,
    };
    const mine: MyVotes = {
      picks: [...current.mine.picks, number],
      revision: current.mine.revision + 1,
    };
    current = { ...current, questions: [...current.questions, question], mine };
    bumpList();
    return { status: 201, json: { question, mine } };
  };

  const answerVote = (request: VoteRequest): Answer => {
    if (current.room.round === null) return { status: 409, json: { error: 'questions_off' } };
    if (request.round !== current.room.round || request.revision !== current.mine.revision)
      return { status: 409, json: { error: 'stale', current } };
    if (new Set(request.picks).size !== request.picks.length)
      return { status: 400, json: { error: 'picks: duplicate' } };
    const byNumber = new Map(current.questions.map((q) => [q.number, q]));
    const unknown = request.picks.find((n) => !byNumber.has(n));
    if (unknown !== undefined)
      return { status: 400, json: { error: `picks: unknown question ${unknown}` } };
    const votable = (n: number) => byNumber.get(n)?.state === 'open';
    const before = new Set(current.mine.picks);
    const wanted = new Set(request.picks);
    const added = [...wanted].filter((n) => !before.has(n) && votable(n));
    const removed = [...before].filter((n) => !wanted.has(n) && votable(n));
    if (accepted.voteChanges + added.length + removed.length > VOTE_CHANGES_PER_MINUTE)
      return { status: 429, json: { error: 'too_many_votes' } };
    accepted.voteChanges += added.length + removed.length;
    const picks = [...before, ...added].filter((n) => !removed.includes(n)).sort((a, b) => a - b);
    const mine: MyVotes = { picks, revision: current.mine.revision + 1 };
    const questions = current.questions.map((q) => ({
      ...q,
      votes: q.votes + (added.includes(q.number) ? 1 : 0) - (removed.includes(q.number) ? 1 : 0),
    }));
    current = { ...current, questions, mine };
    return { status: 200, json: mine };
  };

  const answerTap = (request: ReactionRequest): Answer => {
    if (!REACTION_EMOJI.includes(request.emoji as ReactionEmoji))
      return { status: 400, json: { error: 'emoji: unknown' } };
    if (!current.room.reactions) return { status: 409, json: { error: 'reactions_off' } };
    if (accepted.taps >= TAPS_PER_FIVE_SECONDS)
      return { status: 429, json: { error: 'too_many_reactions' } };
    accepted.taps += 1;
    return { status: 204 };
  };

  const fulfil = async (route: Route, reply: StubQaReply | undefined, server: () => Answer) => {
    const answer = async () => {
      if (reply?.networkDown) return route.abort('connectionrefused');
      const scripted = reply && (reply.status !== undefined || reply.json !== undefined);
      const { status, json } = scripted
        ? { status: reply.status ?? 200, json: reply.json }
        : server();
      return json === undefined ? route.fulfill({ status }) : route.fulfill({ status, json });
    };
    if (reply?.hold) await new Promise<void>((done) => held.push(() => answer().then(done)));
    else await answer();
  };

  const path = `**/api/join/${encodeURIComponent(code)}/questions`;
  await page.route(path, async (route) => {
    if (route.request().method() === 'GET') {
      gets += 1;
      return fulfil(route, queues.get.shift(), () => ({ status: 200, json: current }));
    }
    const request = route.request().postDataJSON() as AskRequest;
    asks.push(request);
    return fulfil(route, queues.ask.shift(), () => answerAsk(request));
  });
  await page.route(`${path}/votes`, async (route) => {
    const request = route.request().postDataJSON() as VoteRequest;
    votes.push(request);
    await fulfil(route, queues.vote.shift(), () => answerVote(request));
  });
  await page.route(`**/api/join/${encodeURIComponent(code)}/reactions`, async (route) => {
    const request = route.request().postDataJSON() as ReactionRequest;
    taps.push(request);
    await fulfil(route, queues.react.shift(), () => answerTap(request));
  });

  return {
    // How many GETs the page made.
    get gets() {
      return gets;
    },
    // Each POST's body, PUT's body and tap's body, in order.
    asks,
    votes,
    taps,
    // The view as the stub holds it now, with every accepted ask and vote.
    get view() {
      return structuredClone(current);
    },
    // Replaces the view: another room (closed, offHere, off, a new round), questions answered or hidden.
    setView(next: GuestQuestionsView) {
      current = structuredClone(next);
    },
    // Replaces only the room, as a settings change or a move to another slide does.
    setRoom(room: QaRoom) {
      current = { ...current, room: structuredClone(room) };
    },
    // Forgets the calls counted against the limits, as a minute (or 5 seconds) passing does.
    resetLimits() {
      accepted = { asks: 0, voteChanges: 0, taps: 0 };
    },
    // The next call of that route answers with reply instead of being checked.
    queueGet(reply: StubQaReply) {
      queues.get.push(reply);
    },
    queueAsk(reply: StubQaReply) {
      queues.ask.push(reply);
    },
    queueVote(reply: StubQaReply) {
      queues.vote.push(reply);
    },
    queueReaction(reply: StubQaReply) {
      queues.react.push(reply);
    },
    // Answers every held call, in the order they arrived; an unscripted one is checked against the view as it is now.
    async release() {
      for (const answer of held.splice(0)) await answer();
    },
  };
}
