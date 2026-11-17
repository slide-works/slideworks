import type { Page } from 'playwright/test';
import {
  QA_ACTIONS,
  type QaAction,
  type QaControlView,
  type QaCounts,
  type QaQuestion,
  type QaRefusalCode,
  type QaSettingsPatch,
  type QaSlidePatch,
  type QaSlideView,
} from '../qa/types';
import { qaControlView, questionControl } from './fixtures/qaStates';
import type { stubDeckHub } from './stubDeckHub';

// One of the presenter's Q&A routes: 'read' for the GET, a moderation action's POST, or a settings PUT.
export type QaCall = 'read' | QaAction | 'settings' | 'slide';

// A scripted answer that replaces the stub's own for a route: a status and body, or a dropped connection.
export type StubbedQaResponse = { status?: number; json?: unknown } | 'network-down';

// One call the page made: the route, the question number or slide number in its path, and the body it sent.
export type QaControlApiCall = { call: QaCall; number?: number; slide?: number; body?: unknown };

export type StubQaControlOptions = {
  // The deck's view as the server holds it now. Default: the qa-control.json "live" sample. Its `live` says whether
  // the deck has an open audience session.
  control?: QaControlView;
  // Scripted answers, per route, that replace the stub's own for every call of that route. Nothing changes state.
  routes?: Partial<Record<QaCall, StubbedQaResponse>>;
};

const ACTIONS: readonly string[] = QA_ACTIONS;

const NO_REACTIONS = { thumbsUp: 0, heart: 0, laugh: 0, wow: 0, clap: 0 };

// How many questions are in each of the panel's tabs: a hidden question counts as hidden only.
function countsOf(questions: QaQuestion[]): QaCounts {
  const counts: QaCounts = { open: 0, waiting: 0, answered: 0, hidden: 0 };
  for (const q of questions) {
    if (q.hidden) counts.hidden += 1;
    else if (q.state === 'waiting') counts.waiting += 1;
    else if (q.state === 'open') counts.open += 1;
    else counts.answered += 1;
  }
  return counts;
}

// The view after the questions changed: counts and onScreen follow the questions.
function withQuestions(view: QaControlView, questions: QaQuestion[]): QaControlView {
  return {
    ...view,
    questions,
    counts: countsOf(questions),
    onScreen: questions.find((q) => q.onScreen)?.number ?? null,
  };
}

// The view with no round: not live, or questions off.
function withoutRound(view: QaControlView): QaControlView {
  return { ...withQuestions(view, []), round: null };
}

// D13: what each action needs, and the question after it. null is a refusal with wrong_state.
function moderate(action: QaAction, q: QaQuestion): QaQuestion | null {
  switch (action) {
    case 'approve':
      return q.state === 'waiting' && !q.hidden ? { ...q, state: 'open' } : null;
    case 'hide':
      return q.state !== 'answered' && !q.hidden ? { ...q, hidden: true, onScreen: false } : null;
    case 'restore':
      return q.hidden ? { ...q, hidden: false } : null;
    case 'answer':
      return q.state === 'open' && !q.hidden ? { ...q, state: 'answered', onScreen: false } : null;
    case 'reopen':
      return q.state === 'answered' ? { ...q, state: 'open' } : null;
    case 'show':
      return q.state === 'open' && !q.hidden ? { ...q, onScreen: true } : null;
    case 'unshow':
      return q.onScreen ? { ...q, onScreen: false } : null;
  }
}

// A slide's switch after a patch, from what the view lists (a slide not listed has both on).
function switchAfter(view: QaControlView, slide: number, patch: QaSlidePatch): QaSlideView {
  const before = view.slides.find((s) => s.slide === slide) ?? {
    slide,
    questions: true,
    reactions: true,
  };
  return {
    slide,
    questions: patch.questions ?? before.questions,
    reactions: patch.reactions ?? before.reactions,
  };
}

// Answers the presenter's Q&A routes for deckId at the network boundary (SW-9 Q6) the way the server will: GET
// /api/decks/{id}/qa, POST …/qa/questions/{number}/{action} for the seven actions, PUT …/qa/settings and PUT
// …/qa/slides/{slide}, over one QaControlView it keeps for the deck. Each question route checks its rules before it
// changes anything and refuses with 409 { error } and a QA_REFUSALS code, changing nothing:
// - every action: not_live while the deck isn't live; an unknown number is 404 { error: 'not_found' } (the stub's
//   wording; the server's text is its own).
// - approve: a waiting question that isn't hidden (a hidden one is restored first). hide: a waiting or open question
//   that isn't hidden; it also comes off screen. restore: a hidden question, back in the state under it. answer: an
//   open question that isn't hidden; it comes off screen. reopen: an answered question. show: an open question that
//   isn't hidden; it replaces any other on screen. unshow: the question on screen. Anything else is wrong_state (D13).
// - settings and a slide's switch work live or not. Questions off ends the round (no round, no questions); leaving off
//   while live starts a new, empty round. A slide whose switch has both on leaves `slides`, and `here` follows when it
//   is the presenter's slide.
// Every answer, a read included, carries the next seq, as the server stamps each view. Returns each call the page made,
// the view, setters for it, and hold() to answer a route slowly.
export async function stubQaControlApi(
  page: Page,
  deckId: string,
  { control = qaControlView('live'), routes = {} }: StubQaControlOptions = {},
) {
  let view = structuredClone(control);
  let rounds = 0;
  const calls: QaControlApiCall[] = [];
  const holds = new Map<QaCall, Promise<void>[]>();

  const stamp = (next: QaControlView) => {
    view = { ...next, seq: view.seq + 1 };
    return { status: 200, json: view };
  };
  const refuse = (error: QaRefusalCode) => ({ status: 409, json: { error } });
  const newRound = () => `round-${++rounds}`;

  function act(action: QaAction, number: number) {
    if (!view.live) return refuse('not_live');
    const question = view.questions.find((q) => q.number === number);
    if (question === undefined) return { status: 404, json: { error: 'not_found' } };
    const after = moderate(action, question);
    if (after === null) return refuse('wrong_state');
    const questions = view.questions.map((q) =>
      q.number === number ? after : action === 'show' ? { ...q, onScreen: false } : q,
    );
    return stamp(withQuestions(view, questions));
  }

  function settle(patch: QaSettingsPatch) {
    const settings = { ...view.settings, ...patch };
    const next = { ...view, settings };
    if (settings.questions === 'off') return stamp(withoutRound(next));
    if (view.live && view.round === null) return stamp({ ...next, round: newRound() });
    return stamp(next);
  }

  function setSlide(slide: number, patch: QaSlidePatch) {
    const after = switchAfter(view, slide, patch);
    const others = view.slides.filter((s) => s.slide !== slide);
    const slides = after.questions && after.reactions ? others : [...others, after];
    slides.sort((a, b) => a.slide - b.slide);
    const here = view.here?.slide === slide ? after : view.here;
    return stamp({ ...view, slides, here });
  }

  function answer(entry: QaControlApiCall): StubbedQaResponse {
    const scripted = routes[entry.call];
    if (scripted !== undefined) return scripted;
    switch (entry.call) {
      case 'read':
        return stamp(view);
      case 'settings':
        return settle(entry.body as QaSettingsPatch);
      case 'slide':
        return setSlide(entry.slide!, entry.body as QaSlidePatch);
      default:
        return act(entry.call, entry.number!);
    }
  }

  const base = `/api/decks/${encodeURIComponent(deckId)}/qa`;
  const path = new RegExp(
    `^${base.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?:/questions/(\\d+)/(\\w+)|/settings|/slides/(\\d+))?$`,
  );
  await page.route(
    (url) => path.test(url.pathname),
    async (route) => {
      const request = route.request();
      const pathname = new URL(request.url()).pathname;
      const [, numberText, action, slideText] = path.exec(pathname)!;
      const body = request.postData() === null ? undefined : request.postDataJSON();
      let entry: QaControlApiCall;
      if (action !== undefined) {
        if (!ACTIONS.includes(action)) return route.fulfill({ status: 404, json: {} });
        entry = { call: action as QaAction, number: Number(numberText) };
      } else if (slideText !== undefined) entry = { call: 'slide', slide: Number(slideText), body };
      else if (pathname.endsWith('/settings')) entry = { call: 'settings', body };
      else entry = { call: 'read' };
      calls.push(entry);
      // The answer is worked out when the call arrives, as the server would; a hold only delays its delivery.
      const response = answer(entry);
      const held = holds.get(entry.call)?.shift();
      if (held !== undefined) await held;
      if (response === 'network-down') await route.abort('connectionrefused');
      else await route.fulfill({ status: response.status ?? 200, json: response.json ?? {} });
    },
  );

  return {
    // Each call the page made, in order, refused and scripted ones included.
    calls,
    // The view as the stub holds it now.
    control: () => view,
    // Replaces the view, as a guest asking or upvoting, or another presenter, would change it. Its seq is kept if it is
    // higher than the stub's.
    setControl(next: QaControlView) {
      view = { ...structuredClone(next), seq: Math.max(view.seq, next.seq) };
    },
    // Opens or ends the audience session. Going live starts a new, empty round unless questions are off, with `here`
    // the presenter's slide (both on unless listed); ending it leaves no round, no questions and no reactions.
    setLive(next: boolean, slide = 1) {
      if (!next) {
        view = { ...withoutRound(view), live: false, here: null, reactions: NO_REACTIONS };
        return;
      }
      view = withQuestions(
        {
          ...view,
          live: true,
          round: view.settings.questions === 'off' ? null : newRound(),
          here: switchAfter(view, slide, {}),
          reactions: NO_REACTIONS,
        },
        [],
      );
    },
    // Holds the answer to the next call of `call` until release(): a slow server. The state still changes on arrival.
    hold(call: QaCall) {
      let release!: () => void;
      const released = new Promise<void>((resolve) => (release = resolve));
      holds.set(call, [...(holds.get(call) ?? []), released]);
      return { release };
    },
  };
}

// Sends a questionControl event to the page's newest hub connection, as the publisher does to the presenter's screens.
export function pushQaControl(
  hub: Pick<Awaited<ReturnType<typeof stubDeckHub>>, 'pushAudienceEvent'>,
  control: QaControlView,
) {
  hub.pushAudienceEvent(questionControl(control));
}
