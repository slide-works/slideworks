import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter, Route, Routes, useNavigate } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { GuestPage } from './GuestPage';
import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

const fake = vi.hoisted(() => {
  const connection = {
    start: vi.fn(async () => undefined),
    stop: vi.fn(async () => undefined),
    state: 'Connected' as string,
    invoke: vi.fn(),
    on: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
  };
  return { connection, get: vi.fn() };
});

vi.mock('../realtime/audienceConnection', () => ({
  createAudienceConnection: () => fake.connection,
}));
vi.mock('../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/client')>()),
  api: { get: fake.get },
}));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
let container: HTMLElement;
// A button that moves the router to the other code, as a link in the app would.
function Navigator() {
  const navigate = useNavigate();
  return createElement('button', { 'data-testid': 'go', onClick: () => navigate('/j/222222') });
}

beforeEach(async () => {
  vi.clearAllMocks();
  fake.connection.state = 'Connected';
  fake.connection.invoke.mockResolvedValue(audienceState({ phase: 'lobby', current: null }));
  container = document.createElement('div');
  root = createRoot(container);
});

afterEach(() => act(() => root.unmount()));

const testId = (id: string) => container.querySelector(`[data-testid="${id}"]`);

describe('GuestPage', () => {
  it('ignores a retry that finishes after the code in the URL changed', async () => {
    const lobby: GuestView = guestView('lobby');
    let finishOldRetry!: (error: Error) => void;
    // The first read for the old code fails, the retry for it hangs, and the new code reads fine.
    fake.get.mockImplementation((path: string) => {
      if (path.endsWith('/111111')) {
        return fake.get.mock.calls.filter(([p]) => p === path).length === 1
          ? Promise.reject(new ApiError('boom', 500))
          : new Promise((_resolve, reject) => (finishOldRetry = reject));
      }
      return Promise.resolve(lobby);
    });
    await act(async () =>
      root.render(
        createElement(
          MemoryRouter,
          { initialEntries: ['/j/111111'] },
          createElement(Navigator),
          createElement(
            Routes,
            null,
            createElement(Route, { path: '/j/:code', element: createElement(GuestPage) }),
          ),
        ),
      ),
    );
    expect(testId(GUEST_TEST_IDS.offline)).not.toBeNull();

    // Try now: the retry for 111111 is in flight.
    await act(async () => (testId(GUEST_TEST_IDS.offlineRetry) as HTMLButtonElement).click());
    expect(finishOldRetry).toBeDefined();

    await act(async () => (testId('go') as HTMLButtonElement).click());
    expect(testId(GUEST_TEST_IDS.lobby)).not.toBeNull();

    // The old retry fails now: it must not replace the new code's page with a stale result.
    await act(async () => finishOldRetry(new ApiError('boom', 500)));
    expect(testId(GUEST_TEST_IDS.lobby)).not.toBeNull();
    expect(testId(GUEST_TEST_IDS.loading)).toBeNull();
    expect(testId(GUEST_TEST_IDS.offline)).toBeNull();
  });
});
