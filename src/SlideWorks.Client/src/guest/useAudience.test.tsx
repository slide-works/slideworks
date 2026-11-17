import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import type { AudienceState } from './audienceEvents';
import { useAudience, type Audience } from './useAudience';

// A hub connection the test drives by hand.
const fake = vi.hoisted(() => {
  const handlers: Record<string, (...args: unknown[]) => void> = {};
  const connection = {
    start: vi.fn(async () => undefined),
    stop: vi.fn(async () => undefined),
    invoke: vi.fn(),
    on: vi.fn((name: string, handler: (...args: unknown[]) => void) => (handlers[name] = handler)),
    onreconnecting: vi.fn((handler: () => void) => (handlers.reconnecting = handler)),
    onreconnected: vi.fn((handler: () => void) => (handlers.reconnected = handler)),
    onclose: vi.fn((handler: () => void) => (handlers.close = handler)),
  };
  return { connection, handlers, get: vi.fn() };
});

vi.mock('../realtime/audienceConnection', () => ({
  createAudienceConnection: () => fake.connection,
}));
vi.mock('../api/client', () => ({ api: { get: fake.get } }));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
const seen = { current: null as unknown as Audience };
const latest = new Proxy({} as Audience, { get: (_t, key) => seen.current[key as keyof Audience] });
const initial = guestView('live');

function Probe({ report }: { report: (audience: Audience) => void }) {
  const audience = useAudience('482913', initial);
  useEffect(() => report(audience));
  return null;
}
const report = (audience: Audience) => {
  seen.current = audience;
};

const flush = () => act(async () => undefined);
const push = (state: AudienceState) => act(async () => void fake.handlers.audienceEvent(state));
const hubRefusal = (message: string) =>
  new Error(
    `An unexpected error occurred invoking 'JoinAudience' on the server. HubException: ${message}`,
  );

beforeEach(async () => {
  vi.clearAllMocks();
  fake.connection.invoke.mockResolvedValue(audienceState({ seq: 50 }));
  fake.get.mockResolvedValue(initial);
  root = createRoot(document.createElement('div'));
  await act(async () => root.render(createElement(Probe, { report })));
  await flush();
});

afterEach(() => act(() => root.unmount()));

describe('useAudience', () => {
  it('reports live only after JoinAudience answers, landing on the state it returns', async () => {
    expect(fake.connection.invoke).toHaveBeenCalledExactlyOnceWith('JoinAudience', '482913');
    expect(latest.status).toBe('live');
    expect(latest.state.seq).toBe(50);
  });

  it('applies a newer audienceState and ignores one that is not newer', async () => {
    await push(audienceState({ seq: 51, revision: 7 }));
    expect(latest.state.seq).toBe(51);

    await push(audienceState({ seq: 51, revision: 7 }));
    await push(audienceState({ seq: 40, revision: 7 }));
    expect(latest.state.seq).toBe(51);
  });

  it('re-reads the view when the revision changes and passes the new view through', async () => {
    const next = { ...guestView('live'), deckTitle: 'Renamed' };
    fake.get.mockResolvedValue(next);

    await push(audienceState({ seq: 51, revision: 8 }));

    expect(fake.get).toHaveBeenCalledExactlyOnceWith('/api/join/482913');
    expect(latest.view.deckTitle).toBe('Renamed');
  });

  it('does not re-read while the revision stays the same', async () => {
    await push(audienceState({ seq: 51, revision: initial.state.revision }));
    expect(fake.get).not.toHaveBeenCalled();
  });

  it('shows reconnecting, rejoins on reconnect and lands on the current state, never replaying', async () => {
    await act(async () => fake.handlers.reconnecting());
    expect(latest.status).toBe('reconnecting');

    fake.connection.invoke.mockResolvedValue(audienceState({ seq: 90 }));
    await act(async () => fake.handlers.reconnected());

    expect(fake.connection.invoke).toHaveBeenCalledTimes(2);
    expect(latest.status).toBe('live');
    expect(latest.state.seq).toBe(90);
  });

  it('reports offline when the connection closes after the retries run out', async () => {
    await act(async () => fake.handlers.close());
    expect(latest.status).toBe('offline');
  });

  it('reports offline when the connection cannot start', async () => {
    await act(() => root.unmount());
    fake.connection.start.mockRejectedValueOnce(new Error('blocked'));
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();
    expect(latest.status).toBe('offline');
  });

  it.each(['no_session', 'too_many_attempts'])(
    'reports offline when JoinAudience says %s',
    async (code) => {
      await act(() => root.unmount());
      fake.connection.invoke.mockRejectedValueOnce(hubRefusal(code));
      root = createRoot(document.createElement('div'));
      await act(async () => root.render(createElement(Probe, { report })));
      await flush();
      expect(latest.status).toBe('offline');
    },
  );

  it('shows the ended phase on audienceEnded', async () => {
    await act(async () => fake.handlers.audienceEvent({ type: 'audienceEnded' }));
    expect(latest.state.phase).toBe('ended');
    expect(latest.state.current).toBeNull();
  });
});
