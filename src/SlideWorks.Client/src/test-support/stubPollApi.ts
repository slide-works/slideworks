import type { Page, Route } from 'playwright/test';
import type { AnswerRequest, GuestPollView, MyAnswer } from '../polls/types';

// One scripted answer to a poll call. With status or json it answers with them (status 200 when left out), such as
// { status: 429, json: answerTooMany() } or { status: 409, json: answerStale() }; networkDown fails the call before
// it reaches the server; with neither it answers as the server would. hold keeps it unanswered until release().
export type StubPollReply = {
  status?: number;
  json?: unknown;
  networkDown?: boolean;
  hold?: boolean;
};

// What the stub tells a hub about the guest after each change: whether WatchPoll would be allowed (V5: the poll is open,
// its results are afterAnswer and this guest has an answer in the round). stubDeckHub's return value fits.
export type StubPollWatchGate = { setPollWatchable(watchable: boolean): void };

export interface StubPollApiOptions {
  // The join code the page is on. '482913' when left out, the guest fixtures' code.
  code?: string;
  // The guest's poll view for the code: what the GET answers and what a PUT is checked against. No poll when left out.
  view?: GuestPollView;
  // A hub stub to keep WatchPoll's rule in step with the view.
  hub?: StubPollWatchGate;
}

const NO_POLL: GuestPollView = { activity: null, mine: null, tally: null, joinedLate: false };

type Answer = { status: number; json: unknown };

// V5's rules for the picks of an open poll, in order, or null when they pass.
function picksRule(view: GuestPollView, picks: unknown): string | null {
  const activity = view.activity!;
  if (!Array.isArray(picks) || picks.length === 0) return 'picks: required';
  const unknown = picks.find(
    (p) => !Number.isInteger(p) || p < 0 || p >= activity.options.length,
  ) as number | undefined;
  if (unknown !== undefined) return `picks: unknown option ${unknown}`;
  if (new Set(picks).size !== picks.length) return 'picks: duplicate';
  if (activity.pick === 'one' && picks.length > 1) return 'picks: pick one';
  if (activity.pick === 'upTo' && picks.length > activity.max!)
    return `picks: at most ${activity.max}`;
  return null;
}

// Component tests only: plays the guest poll routes (V5) at the network boundary for one code, holding that guest's
// GuestPollView. GET /api/join/{code}/poll answers with the view. PUT /api/join/{code}/poll/answer is checked as the
// server checks it: 409 { error: 'stale', current: view } when the poll isn't open, the round isn't its round or the
// revision isn't the guest's current one (0 before a first answer), then 400 { error } naming the first broken picks
// rule (picks are 0-based positions); otherwise 200 { picks, revision + 1 }, stored as the view's mine so the next
// GET returns it. Either route can be scripted instead with queueGet and queueAnswer. Every call is recorded, held and
// failed ones included. Call it before mount().
export async function stubPollApi(
  page: Page,
  { code = '482913', view, hub }: StubPollApiOptions = {},
) {
  let current = structuredClone(view ?? NO_POLL);
  const getQueue: StubPollReply[] = [];
  const answerQueue: StubPollReply[] = [];
  const held: (() => Promise<void>)[] = [];
  let gets = 0;
  const answers: AnswerRequest[] = [];

  const tellHub = () =>
    hub?.setPollWatchable(
      current.activity?.phase === 'open' &&
        current.activity.results === 'afterAnswer' &&
        current.mine !== null,
    );
  tellHub();

  const answerGet = (): Answer => ({ status: 200, json: current });
  const answerPut = (request: AnswerRequest): Answer => {
    const activity = current.activity;
    if (
      activity?.phase !== 'open' ||
      request.round !== activity.round ||
      request.revision !== (current.mine?.revision ?? 0)
    )
      return { status: 409, json: { error: 'stale', current } };
    const rule = picksRule(current, request.picks);
    if (rule !== null) return { status: 400, json: { error: rule } };
    const mine: MyAnswer = {
      picks: [...request.picks].sort((a, b) => a - b),
      revision: request.revision + 1,
    };
    current = { ...current, mine };
    tellHub();
    return { status: 200, json: mine };
  };

  const fulfil = async (route: Route, reply: StubPollReply | undefined, server: () => Answer) => {
    const answer = async () => {
      if (reply?.networkDown) return route.abort('connectionrefused');
      const scripted = reply && (reply.status !== undefined || reply.json !== undefined);
      const { status, json } = scripted
        ? { status: reply.status ?? 200, json: reply.json ?? {} }
        : server();
      return route.fulfill({ status, json });
    };
    if (reply?.hold) await new Promise<void>((done) => held.push(() => answer().then(done)));
    else await answer();
  };

  const path = `**/api/join/${encodeURIComponent(code)}/poll`;
  await page.route(path, async (route) => {
    gets += 1;
    await fulfil(route, getQueue.shift(), answerGet);
  });
  await page.route(`${path}/answer`, async (route) => {
    const request = route.request().postDataJSON() as AnswerRequest;
    answers.push(request);
    await fulfil(route, answerQueue.shift(), () => answerPut(request));
  });

  return {
    // How many GETs the page made.
    get gets() {
      return gets;
    },
    // Each PUT's body, in order.
    answers,
    // The view as the stub holds it now, with any accepted answer.
    get view() {
      return structuredClone(current);
    },
    // The next GET answers with reply instead of the view.
    queueGet(reply: StubPollReply) {
      getQueue.push(reply);
    },
    // The next PUT answers with reply instead of being checked.
    queueAnswer(reply: StubPollReply) {
      answerQueue.push(reply);
    },
    // Replaces the view: a new round, a closed poll, a revealed one.
    setView(next: GuestPollView) {
      current = structuredClone(next);
      tellHub();
    },
    // Answers every held call, in the order they arrived; an unscripted one is checked against the view as it is now.
    async release() {
      for (const answer of held.splice(0)) await answer();
    },
  };
}
