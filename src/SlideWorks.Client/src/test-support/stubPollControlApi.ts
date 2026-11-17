import type { Page } from 'playwright/test';
import type { PollAction } from '../polls/presenter/usePollControl';
import type { PollControlView } from '../polls/types';
import { pollControl, pollControlView } from './fixtures/pollStates';
import type { stubDeckHub } from './stubDeckHub';

// What one poll route answers: a status and body, a dropped connection, or a function of the slide asked for.
export type StubbedPollResponse = { status?: number; json?: unknown } | 'network-down';
export type StubbedPollRoute = StubbedPollResponse | ((slide: number) => StubbedPollResponse);

// One call the page made: the route ('read' for the GET) and the slide number in its path.
export type PollControlCall = { call: 'read' | PollAction; slide: number };

const ACTION_OF_PATH: Readonly<Record<string, PollAction>> = {
  open: 'open',
  close: 'close',
  reveal: 'reveal',
  answer: 'showAnswer',
  round: 'newRound',
};

// What each route answers when a test doesn't say: the poll-control.json sample of the phase the call leads to.
function defaults(): Record<'read' | PollAction, StubbedPollResponse> {
  const open = pollControlView('open');
  const closed = pollControlView('closed');
  return {
    read: { json: open },
    open: { json: open },
    close: { json: closed },
    reveal: { json: { ...open, revealed: true, roomSeesCounts: true } },
    showAnswer: { json: { ...closed, answerShown: true } },
    newRound: { json: { ...open, round: 'round-new', number: closed.number + 1, answered: 0 } },
  };
}

// Answers the presenter's six poll routes for deckId at the network boundary (SW-6 V4): GET
// /api/decks/{id}/polls/{slide} and POST …/open, /close, /reveal, /answer and /round. A refusal is
// { status: 409, json: { error: 'wrong_phase' } }. Returns each call the page made, in order.
export async function stubPollControlApi(
  page: Page,
  deckId: string,
  routes: Partial<Record<'read' | PollAction, StubbedPollRoute>> = {},
) {
  const answers = { ...defaults(), ...routes };
  const calls: PollControlCall[] = [];
  const path = new RegExp(
    `/api/decks/${encodeURIComponent(deckId)}/polls/(\\d+)(?:/(open|close|reveal|answer|round))?$`,
  );
  await page.route(path, async (route) => {
    const [, slideText, actionPath] = path.exec(new URL(route.request().url()).pathname)!;
    const slide = Number(slideText);
    const call = actionPath === undefined ? 'read' : ACTION_OF_PATH[actionPath];
    calls.push({ call, slide });
    const answer = answers[call];
    const response = typeof answer === 'function' ? answer(slide) : answer;
    if (response === 'network-down') await route.abort('connectionrefused');
    else await route.fulfill({ status: response.status ?? 200, json: response.json ?? {} });
  });
  return { calls };
}

// Sends a pollControl event to the page's newest hub connection, as the publisher does to the presenter's screens.
export function pushPollControl(
  hub: Pick<Awaited<ReturnType<typeof stubDeckHub>>, 'pushAudienceEvent'>,
  control: PollControlView,
) {
  hub.pushAudienceEvent(pollControl(control));
}
