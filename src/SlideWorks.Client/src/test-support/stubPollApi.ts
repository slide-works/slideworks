import type { Page, Route } from 'playwright/test';
import type { AnswerRequest, GuestPollView } from '../polls/types';

// One answer to a poll call: a status and a body (200 when left out), a connection that never reached the server, or a
// response held until release().
export type StubPollReply = { status?: number; json?: unknown } | 'network-down' | 'hold';

export interface StubPollApiOptions {
  // The join code the page is on. '482913' when left out, the guest fixtures' code.
  code?: string;
  // What GET /api/join/{code}/poll answers when no reply is queued. The "noPoll" view when left out.
  view?: GuestPollView;
}

const NO_POLL: GuestPollView = { activity: null, mine: null, tally: null };

// Component tests only: plays the guest poll routes (V5) at the network boundary. GET /api/join/{code}/poll and PUT
// /api/join/{code}/poll/answer each take the next queued reply; with none queued the GET answers with the view, and the
// PUT accepts the picks and answers { picks, revision: revision + 1 }, as the server does for an accepted answer. Every
// call is recorded, held and failed ones included. Call it before mount().
export async function stubPollApi(page: Page, { code = '482913', view }: StubPollApiOptions = {}) {
  let current = structuredClone(view ?? NO_POLL);
  const getQueue: StubPollReply[] = [];
  const answerQueue: StubPollReply[] = [];
  const held: (() => Promise<void>)[] = [];
  let gets = 0;
  const answers: AnswerRequest[] = [];

  const fulfil = async (route: Route, reply: StubPollReply, fallback: () => unknown) => {
    if (reply === 'network-down') await route.abort('connectionrefused');
    else if (reply === 'hold') {
      // A held reply answers, on release(), the way an unqueued one would.
      await new Promise<void>((answered) =>
        held.push(() => route.fulfill({ json: fallback() }).then(answered)),
      );
    } else await route.fulfill({ status: reply.status ?? 200, json: reply.json ?? fallback() });
  };

  const path = `**/api/join/${encodeURIComponent(code)}/poll`;
  await page.route(path, async (route) => {
    gets += 1;
    await fulfil(route, getQueue.shift() ?? { json: current }, () => current);
  });
  await page.route(`${path}/answer`, async (route) => {
    const request = route.request().postDataJSON() as AnswerRequest;
    answers.push(request);
    const accepted = () => ({ picks: request.picks, revision: request.revision + 1 });
    await fulfil(route, answerQueue.shift() ?? { json: accepted() }, accepted);
  });

  return {
    // How many GETs the page made.
    get gets() {
      return gets;
    },
    // Each PUT's body, in order.
    answers,
    // The next GET answers with reply instead of the view.
    queueGet(reply: StubPollReply) {
      getQueue.push(reply);
    },
    // The next PUT answers with reply instead of accepting, such as { status: 409, json: answerStale() }.
    queueAnswer(reply: StubPollReply) {
      answerQueue.push(reply);
    },
    // Changes what later GETs answer with.
    setView(next: GuestPollView) {
      current = structuredClone(next);
    },
    // Answers every held call, in the order they arrived.
    async release() {
      for (const answer of held.splice(0)) await answer();
    },
  };
}
