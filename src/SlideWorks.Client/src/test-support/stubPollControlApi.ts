import type { Page } from 'playwright/test';
import type { PollAction } from '../polls/presenter/usePollControl';
import type { PollControlView, PollRefusalCode } from '../polls/types';
import { pollControl, pollControlView } from './fixtures/pollStates';
import type { stubDeckHub } from './stubDeckHub';

// One of the six poll routes: 'read' for the GET, or an action's POST.
export type PollCall = 'read' | PollAction;

// A scripted answer that replaces the stub's own for a route: a status and body, or a dropped connection.
export type StubbedPollResponse = { status?: number; json?: unknown } | 'network-down';
export type StubbedPollRoute = StubbedPollResponse | ((slide: number) => StubbedPollResponse);

// One call the page made: the route and the slide number in its path.
export type PollControlCall = { call: PollCall; slide: number };

export type StubPollControlOptions = {
  // The deck's poll slides in this session, each keyed by its view's `slide`. Default: the poll-control.json "open"
  // sample (slide 4). A slide with no poll here answers 404.
  polls?: PollControlView[];
  // Whether the deck has an open audience session. Default true; Open refuses not_live without one.
  live?: boolean;
  // Scripted answers, per route, that replace the stub's own for every call of that route. Nothing changes state.
  routes?: Partial<Record<PollCall, StubbedPollRoute>>;
};

const ACTION_OF_PATH: Readonly<Record<string, PollAction>> = {
  open: 'open',
  close: 'close',
  reveal: 'reveal',
  answer: 'showAnswer',
  round: 'newRound',
};

// The room sees the counts when the results are live, the round is revealed, or it is closed (PollVisibility.RoomSees).
const roomSees = (view: PollControlView) =>
  view.settings.results === 'live' || view.revealed || view.phase === 'closed';

// A fresh round on the same poll: number + 1, nothing answered, nothing revealed or shown.
function freshRound(view: PollControlView, phase: 'draft' | 'open'): PollControlView {
  const next: PollControlView = {
    ...view,
    number: view.number + 1,
    phase,
    round: phase === 'open' ? `round-${view.slide}-${view.number + 1}` : null,
    answered: 0,
    counts: view.options.map(() => 0),
    revealed: false,
    answerShown: false,
  };
  return { ...next, roomSeesCounts: roomSees(next) };
}

// Answers the presenter's six poll routes for deckId at the network boundary (SW-6 V4) the way the server will: GET
// /api/decks/{id}/polls/{slide} and POST …/open, /close, /reveal, /answer and /round, over poll state it keeps per slide
// for this deck. Each action checks V4's rules before it changes anything and refuses with 409 { error } and a
// POLL_REFUSALS code, changing nothing:
// - open: not_live without a session; wrong_phase unless draft; another_poll_open while another slide's round is open.
//   A draft left by New round on another slide (opens: manual) is discarded first, back to its closed round, as the
//   server discards that draft row (SW-6 amendment to V2 and V4).
// - close: wrong_phase unless open. The round joins `rounds`, and the room sees the counts.
// - reveal: wrong_phase unless open; already_visible when the room already sees the counts.
// - show answer: wrong_phase unless closed; no_correct when the poll has no correct option.
// - new round: wrong_phase unless closed. Round number + 1, open when opens is onArrival and draft when manual.
// Every answer, a read included, carries the next seq, as the server's PollSeq stamps each view. A slide with no poll
// answers 404 { error: 'not_a_poll' } (the stub's wording; the server's text is its own). Returns each call the page
// made, the state, setters for the state, and hold() to answer a route slowly.
export async function stubPollControlApi(
  page: Page,
  deckId: string,
  { polls = [pollControlView('open')], live = true, routes = {} }: StubPollControlOptions = {},
) {
  const views = new Map<number, PollControlView>(polls.map((p) => [p.slide, structuredClone(p)]));
  // The closed view a slide had before a manual New round left it in draft: restored when that draft is discarded.
  const beforeDraft = new Map<number, PollControlView>();
  let isLive = live;
  let seq = Math.max(0, ...polls.map((p) => p.seq));
  const calls: PollControlCall[] = [];
  const holds = new Map<PollCall, Promise<void>[]>();

  const stamp = (view: PollControlView) => {
    seq += 1;
    const stamped = { ...view, seq };
    views.set(view.slide, stamped);
    return stamped;
  };

  const refuse = (error: PollRefusalCode) => ({ status: 409, json: { error } });

  // A draft that New round left on a slide goes back to the closed round before it; its number is used up.
  const discardDraft = (slide: number) => {
    const closed = beforeDraft.get(slide);
    if (views.get(slide)?.phase !== 'draft' || closed === undefined) return;
    beforeDraft.delete(slide);
    stamp(closed);
  };

  // The action's answer: a refusal changes nothing; an accepted action stores and returns the new view.
  function act(action: PollAction, view: PollControlView): { status: number; json: unknown } {
    switch (action) {
      case 'open': {
        if (!isLive) return refuse('not_live');
        if (view.phase !== 'draft') return refuse('wrong_phase');
        const others = [...views.values()].filter((other) => other.slide !== view.slide);
        if (others.some((other) => other.phase === 'open')) return refuse('another_poll_open');
        for (const other of others) discardDraft(other.slide);
        beforeDraft.delete(view.slide);
        const opened: PollControlView = {
          ...view,
          phase: 'open',
          round: `round-${view.slide}-${view.number}`,
          answered: 0,
          counts: view.options.map(() => 0),
          revealed: false,
          answerShown: false,
        };
        return { status: 200, json: stamp({ ...opened, roomSeesCounts: roomSees(opened) }) };
      }
      case 'close': {
        if (view.phase !== 'open') return refuse('wrong_phase');
        const round = { number: view.number, answered: view.answered, joined: view.joined };
        return {
          status: 200,
          json: stamp({
            ...view,
            phase: 'closed',
            roomSeesCounts: true,
            rounds: [...view.rounds, round],
          }),
        };
      }
      case 'reveal':
        if (view.phase !== 'open') return refuse('wrong_phase');
        if (view.roomSeesCounts) return refuse('already_visible');
        return { status: 200, json: stamp({ ...view, revealed: true, roomSeesCounts: true }) };
      case 'showAnswer':
        if (view.phase !== 'closed') return refuse('wrong_phase');
        if (view.settings.correct === null) return refuse('no_correct');
        return { status: 200, json: stamp({ ...view, answerShown: true }) };
      case 'newRound': {
        if (view.phase !== 'closed') return refuse('wrong_phase');
        const manual = view.settings.opens === 'manual';
        if (manual) beforeDraft.set(view.slide, view);
        return { status: 200, json: stamp(freshRound(view, manual ? 'draft' : 'open')) };
      }
    }
  }

  function answer(call: PollCall, slide: number): StubbedPollResponse {
    const scripted = routes[call];
    if (scripted !== undefined) return typeof scripted === 'function' ? scripted(slide) : scripted;
    const view = views.get(slide);
    if (view === undefined) return { status: 404, json: { error: 'not_a_poll' } };
    return call === 'read' ? { status: 200, json: stamp(view) } : act(call, view);
  }

  const path = new RegExp(
    `/api/decks/${encodeURIComponent(deckId)}/polls/(\\d+)(?:/(open|close|reveal|answer|round))?$`,
  );
  await page.route(path, async (route) => {
    const [, slideText, actionPath] = path.exec(new URL(route.request().url()).pathname)!;
    const slide = Number(slideText);
    const call: PollCall = actionPath === undefined ? 'read' : ACTION_OF_PATH[actionPath];
    calls.push({ call, slide });
    // The answer is worked out when the call arrives, as the server would; a hold only delays its delivery.
    const response = answer(call, slide);
    const held = holds.get(call)?.shift();
    if (held !== undefined) await held;
    if (response === 'network-down') await route.abort('connectionrefused');
    else await route.fulfill({ status: response.status ?? 200, json: response.json ?? {} });
  });

  return {
    // Each call the page made, in order, refused and scripted ones included.
    calls,
    // The poll on a slide as the stub holds it now, or undefined for a slide with no poll.
    poll: (slide: number) => views.get(slide),
    // Puts a poll in place (or replaces one), keyed by its slide, as an answer arriving or an agent's edit would.
    setPoll(view: PollControlView) {
      views.set(view.slide, structuredClone(view));
      seq = Math.max(seq, view.seq);
    },
    // Opens or ends the audience session, for not_live. Ending it closes an open round and discards a draft left by New
    // round, as the server does.
    setLive(next: boolean) {
      isLive = next;
      if (next) return;
      for (const view of [...views.values()]) {
        if (view.phase === 'open') act('close', view);
        else discardDraft(view.slide);
      }
    },
    // Holds the answer to the next call of `call` until release(): a slow server. The state still changes on arrival.
    hold(call: PollCall) {
      let release!: () => void;
      const released = new Promise<void>((resolve) => (release = resolve));
      holds.set(call, [...(holds.get(call) ?? []), released]);
      return { release };
    },
  };
}

// Sends a pollControl event to the page's newest hub connection, as the publisher does to the presenter's screens.
export function pushPollControl(
  hub: Pick<Awaited<ReturnType<typeof stubDeckHub>>, 'pushAudienceEvent'>,
  control: PollControlView,
) {
  hub.pushAudienceEvent(pollControl(control));
}
