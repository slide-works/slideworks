import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import controlSample from '@contracts/screens/open-response-control.json' with { type: 'json' };
import { usePresenterPollKeys } from '../../presentation/presenter/usePresenterKeys';
import { openResponseErrorSentence } from '../errors';
import { stubOpenResponses } from '../stubOpenResponses';
import type { GuestResponseView, OpenResponseControlView } from '../types';
import { OpenResponseControlLive } from './OpenResponseControlLive';
import { PresenterOpenPanel } from './PresenterOpenPanel';
import { OPEN_PRESENTER_IDS as IDS } from './testIds';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    configureLogging() {
      return this;
    }
    build() {
      return {
        start: async () => undefined,
        stop: async () => undefined,
        invoke: async () => undefined,
        on: () => undefined,
        onreconnecting: () => undefined,
        onreconnected: () => undefined,
        onclose: () => undefined,
      };
    }
  }
  return { HubConnectionBuilder, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 } };
});

const DECK = 'deck-1';
const SLIDE = 1;

const GUEST: GuestResponseView = {
  phase: 'open',
  round: '6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63',
  number: 1,
  revision: 10,
  question: 'One word for today?',
  revealed: true,
  guests: 12,
  kind: 'word-cloud',
  mineWords: [],
};

type Fulfillment = { status?: number; json?: unknown };

type FakeRoute = {
  request: () => { method: () => string; url: () => string; postDataJSON: () => unknown };
  fulfill: (response: Fulfillment) => Promise<void>;
  fallback: () => Promise<void>;
};

type FetchPage = {
  calls: string[];
  route: (pattern: string, handler: (route: FakeRoute) => unknown) => Promise<void>;
  restore: () => void;
};

function globToRegExp(glob: string): RegExp {
  let source = '^';
  for (let i = 0; i < glob.length; i++) {
    const char = glob[i];
    if (char === '*' && glob[i + 1] === '*') {
      source += '.*';
      i += 1;
    } else if (char === '*') source += '[^/]*';
    else source += /[\\^$+?.()|{}[\]]/.test(char) ? `\\${char}` : char;
  }
  return new RegExp(`${source}$`);
}

function installFetchPage(): FetchPage {
  const routes: { pattern: RegExp; handler: (route: FakeRoute) => unknown }[] = [];
  const calls: string[] = [];
  const previous = globalThis.fetch.bind(globalThis);
  const page: FetchPage = {
    calls,
    async route(pattern, handler) {
      routes.push({ pattern: globToRegExp(pattern), handler });
    },
    restore() {
      globalThis.fetch = previous;
    },
  };
  globalThis.fetch = async (input, init) => {
    const raw = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const url = new URL(raw, window.location.href).href;
    const method = init?.method ?? 'GET';
    const body = typeof init?.body === 'string' ? init.body : undefined;
    calls.push(`${method} ${new URL(url).pathname}`);
    for (let i = routes.length - 1; i >= 0; i--) {
      if (!routes[i].pattern.test(url)) continue;
      let settled: Response | null = null;
      let fell = false;
      const route: FakeRoute = {
        request: () => ({
          method: () => method,
          url: () => url,
          postDataJSON: () => (body === undefined ? null : JSON.parse(body)),
        }),
        fulfill: async (response) => {
          settled = new Response(JSON.stringify(response.json ?? {}), {
            status: response.status ?? 200,
            headers: { 'Content-Type': 'application/json' },
          });
        },
        fallback: async () => {
          fell = true;
        },
      };
      await routes[i].handler(route);
      if (fell) continue;
      return (
        settled ??
        new Response(JSON.stringify({ error: 'stub_no_body' }), {
          status: 500,
          headers: { 'Content-Type': 'application/json' },
        })
      );
    }
    return new Response(JSON.stringify({ error: 'not_found' }), {
      status: 404,
      headers: { 'Content-Type': 'application/json' },
    });
  };
  return page;
}

function press(key: string) {
  window.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
}

async function settle() {
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
}

async function until(ready: () => boolean) {
  for (let i = 0; i < 30 && !ready(); i++) await settle();
}

let root: Root;
let host: HTMLDivElement;
let page: FetchPage;

beforeEach(() => {
  page = installFetchPage();
  host = document.createElement('div');
  document.body.appendChild(host);
  root = createRoot(host);
});

afterEach(async () => {
  await act(() => root.unmount());
  host.remove();
  page.restore();
});

function PollKeys({ onToggle }: { onToggle: () => void }) {
  usePresenterPollKeys({ togglePoll: onToggle }, false);
  return null;
}

test('Keys_WhenTheSlideIsAWordCloud_ShouldToggleOnPAndDoNothingOnR', async () => {
  const control = structuredClone(controlSample.wordCloud) as OpenResponseControlView;
  await page.route(`**/api/decks/${DECK}/slides/${SLIDE}/response/close`, (route) => {
    control.phase = 'closed';
    return route.fulfill({ status: 200, json: structuredClone(control) });
  });
  await stubOpenResponses(page as unknown as Parameters<typeof stubOpenResponses>[0], {
    slide: SLIDE,
    view: GUEST,
    deckId: DECK,
    control,
  });

  await act(async () => {
    root.render(
      <OpenResponseControlLive deckId={DECK} slide={SLIDE}>
        <PresenterOpenPanel deckId={DECK} />
      </OpenResponseControlLive>,
    );
  });
  await until(() => document.querySelector(`[data-testid="${IDS.phase}"]`)?.textContent === 'Open');

  expect(document.querySelector(`[data-testid="${IDS.phase}"]`)?.textContent).toBe('Open');
  await act(async () => press('p'));
  await until(
    () => document.querySelector(`[data-testid="${IDS.phase}"]`)?.textContent === 'Closed',
  );
  expect(document.querySelector(`[data-testid="${IDS.phase}"]`)?.textContent).toBe('Closed');
  expect(page.calls).toContain(`POST /api/decks/${DECK}/slides/${SLIDE}/response/close`);

  const afterToggle = [...page.calls];
  await act(async () => press('r'));
  await settle();
  expect(page.calls).toEqual(afterToggle);
  expect(document.querySelector(`[data-testid="${IDS.phase}"]`)?.textContent).toBe('Closed');
  expect(page.calls.some((call) => call.toLowerCase().includes('reveal'))).toBe(false);
});

test('Keys_WhenTheSlideIsNotOpenResponse_ShouldLeavePAlone', async () => {
  await stubOpenResponses(page as unknown as Parameters<typeof stubOpenResponses>[0], {
    slide: SLIDE,
    view: GUEST,
  });
  let toggled = false;
  await act(async () => {
    root.render(
      <>
        <OpenResponseControlLive deckId={DECK} slide={SLIDE}>
          <PresenterOpenPanel deckId={DECK} />
        </OpenResponseControlLive>
        <PollKeys onToggle={() => (toggled = true)} />
      </>,
    );
  });
  await until(() => page.calls.some((call) => call.startsWith('GET ')));

  await act(async () => press('p'));
  await settle();

  expect(toggled).toBe(true);
  expect(page.calls.some((call) => call.includes('/response/'))).toBe(false);
  expect(document.querySelector(`[data-testid="${IDS.panel}"]`)).toBeNull();
});

test('Panel_WhenHideFails_ShouldShowTheSentence', async () => {
  const control = structuredClone(controlSample.wordCloud) as OpenResponseControlView;
  await page.route(`**/api/decks/${DECK}/slides/${SLIDE}/response/entries/1/hide`, (route) =>
    route.fulfill({ status: 404, json: { error: 'no_entry' } }),
  );
  await stubOpenResponses(page as unknown as Parameters<typeof stubOpenResponses>[0], {
    slide: SLIDE,
    view: GUEST,
    deckId: DECK,
    control,
  });

  await act(async () => {
    root.render(
      <OpenResponseControlLive deckId={DECK} slide={SLIDE}>
        <PresenterOpenPanel deckId={DECK} />
      </OpenResponseControlLive>,
    );
  });
  await until(() => document.querySelector(`[data-testid="${IDS.hide(1)}"]`) !== null);

  const button = document.querySelector(`[data-testid="${IDS.hide(1)}"]`);
  expect(button).toBeInstanceOf(HTMLButtonElement);
  if (!(button instanceof HTMLButtonElement)) return;
  button.focus();
  await act(async () => {
    button.click();
  });
  await until(
    () =>
      document.querySelector(`[data-testid="${IDS.error}"]`)?.textContent ===
      openResponseErrorSentence('no_entry'),
  );

  expect(document.querySelector(`[data-testid="${IDS.error}"]`)?.textContent).toBe(
    openResponseErrorSentence('no_entry'),
  );
  expect(document.activeElement).toBe(document.querySelector(`[data-testid="${IDS.hide(1)}"]`));
});
