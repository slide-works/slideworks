import { act, Component, createElement, useEffect, type ReactNode } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import type { AudienceState } from './audienceEvents';
import { NO_SESSION, TOO_MANY_ATTEMPTS, type GuestView } from './guestView';
import {
  JITTER_MS,
  REREAD_RETRY_MS,
  SLOW_JITTER_MS,
  THROTTLED_RETRY_MS,
  useAudience,
  type Audience,
} from './useAudience';

// A hub connection the test drives by hand.
const fake = vi.hoisted(() => {
  const handlers: Record<string, (...args: unknown[]) => void> = {};
  const connection = {
    start: vi.fn(async () => undefined),
    stop: vi.fn(async () => undefined),
    state: 'Connected' as string,
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
vi.mock('../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/client')>()),
  api: { get: fake.get },
}));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
const seen = { current: null as unknown as Audience };
const latest = new Proxy({} as Audience, { get: (_t, key) => seen.current[key as keyof Audience] });
const initial = guestView('live');
const random = { current: () => 0 };
const start = { current: null as string | null };

function Probe({ report }: { report: (audience: Audience) => void }) {
  const audience = useAudience('482913', initial, () => random.current(), start.current);
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
  vi.useRealTimers();
  fake.connection.state = 'Connected';
  random.current = () => 0;
  start.current = null;
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

  it.each(['no_session'])('reports offline when JoinAudience says %s', async (code) => {
    await act(() => root.unmount());
    fake.connection.invoke.mockRejectedValueOnce(hubRefusal(code));
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();
    expect(latest.status).toBe('offline');
  });

  it('shows the ended phase on audienceEnded', async () => {
    await act(async () => fake.handlers.audienceEvent({ type: 'audienceEnded' }));
    expect(latest.state.phase).toBe('ended');
    expect(latest.state.current).toBeNull();
  });

  it('keeps a state pushed while JoinAudience was in flight when the older answer arrives', async () => {
    await act(() => root.unmount());
    let release!: (state: AudienceState) => void;
    fake.connection.invoke.mockReturnValueOnce(new Promise<AudienceState>((r) => (release = r)));
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();
    expect(latest.status).toBe('connecting');

    await push(audienceState({ seq: 51 }));
    await act(async () => release(audienceState({ seq: 50 })));

    expect(latest.state.seq).toBe(51);
    expect(latest.status).toBe('live');
  });

  it('keeps a state pushed while JoinAudience was in flight when the answer has the same seq', async () => {
    await act(() => root.unmount());
    let release!: (state: AudienceState) => void;
    fake.connection.invoke.mockReturnValueOnce(new Promise<AudienceState>((r) => (release = r)));
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();

    // Go live races the join: the live push arrives first, then the lobby answer with the same seq.
    await push(audienceState({ seq: 51, phase: 'live' }));
    await act(async () => release(audienceState({ seq: 51, phase: 'lobby', current: null })));

    expect(latest.state.phase).toBe('live');
    expect(latest.state.current).not.toBeNull();
    expect(latest.status).toBe('live');
  });

  it('still lands on a lower seq after a reconnect when nothing was pushed meanwhile (server restart)', async () => {
    await push(audienceState({ seq: 51 }));
    await act(async () => fake.handlers.reconnecting());
    fake.connection.invoke.mockResolvedValue(audienceState({ seq: 3 }));
    await act(async () => fake.handlers.reconnected());
    expect(latest.state.seq).toBe(3);
  });

  it('stays reconnecting until the rejoin answers', async () => {
    await act(async () => fake.handlers.reconnecting());
    let release!: (state: AudienceState) => void;
    fake.connection.invoke.mockReturnValueOnce(new Promise<AudienceState>((r) => (release = r)));
    await act(async () => fake.handlers.reconnected());
    expect(latest.status).toBe('reconnecting');

    await act(async () => release(audienceState({ seq: 90 })));
    expect(latest.status).toBe('live');
  });

  it('reports needs_cookies when JoinAudience says no_guest', async () => {
    await act(() => root.unmount());
    fake.connection.invoke.mockRejectedValueOnce(hubRefusal('no_guest'));
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();
    expect(latest.status).toBe('needs_cookies');
  });

  it('throws any other JoinAudience failure for the error boundary', async () => {
    await act(() => root.unmount());
    fake.connection.invoke.mockRejectedValueOnce(hubRefusal('boom'));
    const errors: unknown[] = [];
    class Catch extends Component<{ children: ReactNode }, { failed: boolean }> {
      state = { failed: false };
      static getDerivedStateFromError() {
        return { failed: true };
      }
      componentDidCatch(error: unknown) {
        errors.push(error);
      }
      render() {
        return this.state.failed ? null : this.props.children;
      }
    }
    const quiet = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    root = createRoot(document.createElement('div'));
    await act(async () =>
      root.render(createElement(Catch, null, createElement(Probe, { report }))),
    );
    await flush();
    quiet.mockRestore();
    expect(errors).toHaveLength(1);
  });

  it('does not set offline on a network blip during a re-read, and tries the re-read again', async () => {
    vi.useFakeTimers();
    fake.get.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    fake.get.mockResolvedValueOnce({ ...guestView('live'), deckTitle: 'Renamed' });

    await push(audienceState({ seq: 51, revision: 9 }));
    expect(latest.status).toBe('live');
    expect(latest.view.deckTitle).not.toBe('Renamed');

    await act(async () => vi.advanceTimersByTimeAsync(REREAD_RETRY_MS));
    expect(fake.get).toHaveBeenCalledTimes(2);
    expect(latest.view.deckTitle).toBe('Renamed');
    expect(latest.status).toBe('live');
  });

  it('keeps the screen and tries a throttled re-read again after a slow wait', async () => {
    vi.useFakeTimers();
    random.current = () => 0.5;
    fake.get.mockRejectedValueOnce(new ApiError(TOO_MANY_ATTEMPTS, 429));
    fake.get.mockResolvedValueOnce({ ...guestView('live'), deckTitle: 'Renamed' });

    await push(audienceState({ seq: 51, revision: 9 }));
    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(fake.get).toHaveBeenCalledTimes(1);
    expect(latest.status).toBe('live');

    await act(async () => vi.advanceTimersByTimeAsync(THROTTLED_RETRY_MS - 1));
    expect(fake.get).toHaveBeenCalledTimes(1);
    await act(async () => vi.advanceTimersByTimeAsync(SLOW_JITTER_MS + JITTER_MS));
    expect(fake.get).toHaveBeenCalledTimes(2);
    expect(latest.view.deckTitle).toBe('Renamed');
    expect(latest.status).toBe('live');
  });

  it('goes offline and stops re-reading when a re-read says no_session (the deck is gone)', async () => {
    vi.useFakeTimers();
    fake.get.mockRejectedValue(new ApiError(NO_SESSION, 404));
    await push(audienceState({ seq: 51, revision: 9 }));
    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(latest.status).toBe('offline');

    await act(async () => vi.advanceTimersByTimeAsync(60_000));
    expect(fake.get).toHaveBeenCalledTimes(1);
  });

  it('does not re-read once the talk has ended', async () => {
    vi.useFakeTimers();
    await act(async () => fake.handlers.audienceEvent({ type: 'audienceEnded' }));
    await push(audienceState({ seq: 51, revision: 12, phase: 'ended', current: null }));
    await act(async () => vi.advanceTimersByTimeAsync(60_000));
    expect(fake.get).not.toHaveBeenCalled();
  });

  it('stops a failing re-read when the talk ends meanwhile', async () => {
    vi.useFakeTimers();
    fake.get.mockRejectedValue(new TypeError('Failed to fetch'));
    await push(audienceState({ seq: 51, revision: 9 }));
    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(fake.get).toHaveBeenCalledTimes(1);

    await act(async () => fake.handlers.audienceEvent({ type: 'audienceEnded' }));
    await act(async () => vi.advanceTimersByTimeAsync(60_000));
    expect(fake.get).toHaveBeenCalledTimes(1);
  });

  it('applies audienceEnded whatever the seq of the last live state', async () => {
    await push(audienceState({ seq: 900 }));
    await act(async () => fake.handlers.audienceEvent({ type: 'audienceEnded' }));
    expect(latest.state.phase).toBe('ended');
    expect(latest.state.current).toBeNull();
  });

  it('applies a state with the held seq when its phase differs, and drops an exact repeat', async () => {
    await push(audienceState({ seq: 50, phase: 'lobby', current: null }));
    expect(latest.state.phase).toBe('lobby');

    await push(audienceState({ seq: 50, phase: 'lobby', current: null, revision: 99 }));
    expect(fake.get).not.toHaveBeenCalled();

    await push(audienceState({ seq: 50, phase: 'live' }));
    expect(latest.state.phase).toBe('live');
  });

  it('does not throw when an invoke is cancelled by a reconnect, and stays reconnecting', async () => {
    await act(() => root.unmount());
    let cancel!: (error: Error) => void;
    fake.connection.invoke.mockReturnValueOnce(new Promise<AudienceState>((_, r) => (cancel = r)));
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();

    await act(async () => fake.handlers.reconnecting());
    await act(async () =>
      cancel(new Error('Invocation canceled due to the underlying connection being closed.')),
    );
    expect(latest.status).toBe('reconnecting');
  });

  it('treats an invoke rejected while not connected as a connection error, not a failure', async () => {
    await act(() => root.unmount());
    fake.connection.invoke.mockRejectedValueOnce(
      new Error("Cannot send data if the connection is not in the 'Connected' State."),
    );
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();
    expect(latest.status).toBe('connecting');
  });

  it('makes no call when a throttled rejoin timer would fire after the connection closed', async () => {
    vi.useFakeTimers();
    random.current = () => 0.5;
    await act(async () => fake.handlers.reconnecting());
    fake.connection.invoke.mockRejectedValueOnce(hubRefusal('too_many_attempts'));
    await act(async () => fake.handlers.reconnected());
    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(latest.status).toBe('reconnecting');

    fake.connection.invoke.mockClear();
    await act(async () => fake.handlers.close());
    await act(async () => vi.advanceTimersByTimeAsync(THROTTLED_RETRY_MS + SLOW_JITTER_MS));
    expect(fake.connection.invoke).not.toHaveBeenCalled();
    expect(latest.status).toBe('offline');
  });

  it('makes no call when the connection drops again before the rejoin timer fires', async () => {
    vi.useFakeTimers();
    random.current = () => 0.9;
    fake.connection.invoke.mockClear();
    await act(async () => fake.handlers.reconnecting());
    await act(async () => fake.handlers.reconnected());
    await act(async () => fake.handlers.reconnecting());
    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(fake.connection.invoke).not.toHaveBeenCalled();
    expect(latest.status).toBe('reconnecting');
  });

  it('makes no call when the rejoin timer fires on a connection that is not connected', async () => {
    vi.useFakeTimers();
    random.current = () => 0.9;
    fake.connection.invoke.mockClear();
    await act(async () => fake.handlers.reconnected());
    fake.connection.state = 'Reconnecting';
    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(fake.connection.invoke).not.toHaveBeenCalled();
  });

  it('throws an unnamed HubException for the error boundary', async () => {
    await act(() => root.unmount());
    fake.connection.invoke.mockRejectedValueOnce(
      new Error(
        "An unexpected error occurred invoking 'JoinAudience' on the server. HubException: ",
      ),
    );
    const errors: unknown[] = [];
    class Catch extends Component<{ children: ReactNode }, { failed: boolean }> {
      state = { failed: false };
      static getDerivedStateFromError() {
        return { failed: true };
      }
      componentDidCatch(error: unknown) {
        errors.push(error);
      }
      render() {
        return this.state.failed ? null : this.props.children;
      }
    }
    const quiet = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    root = createRoot(document.createElement('div'));
    await act(async () =>
      root.render(createElement(Catch, null, createElement(Probe, { report }))),
    );
    await flush();
    quiet.mockRestore();
    expect(errors).toHaveLength(1);
  });

  it('waits a random extra before a re-read', async () => {
    vi.useFakeTimers();
    random.current = () => 0.99;
    await push(audienceState({ seq: 51, revision: 9 }));
    expect(fake.get).not.toHaveBeenCalled();
    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(fake.get).toHaveBeenCalledTimes(1);
  });

  it('waits a random extra before rejoining after a reconnect', async () => {
    vi.useFakeTimers();
    random.current = () => 0.9;
    fake.connection.invoke.mockClear();
    await act(async () => fake.handlers.reconnecting());
    await act(async () => fake.handlers.reconnected());
    expect(fake.connection.invoke).not.toHaveBeenCalled();
    expect(latest.status).toBe('reconnecting');

    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(fake.connection.invoke).toHaveBeenCalledTimes(1);
    expect(latest.status).toBe('live');
  });

  it('keeps the screen with the banner and rejoins later when the rejoin is throttled', async () => {
    vi.useFakeTimers();
    random.current = () => 0.5;
    await act(async () => fake.handlers.reconnecting());
    fake.connection.invoke.mockRejectedValueOnce(hubRefusal('too_many_attempts'));
    fake.connection.invoke.mockResolvedValueOnce(audienceState({ seq: 70 }));
    await act(async () => fake.handlers.reconnected());
    await act(async () => vi.advanceTimersByTimeAsync(JITTER_MS));
    expect(latest.status).toBe('reconnecting');
    expect(latest.state.seq).toBe(50);

    await act(async () => vi.advanceTimersByTimeAsync(THROTTLED_RETRY_MS + SLOW_JITTER_MS));
    expect(latest.status).toBe('live');
    expect(latest.state.seq).toBe(70);
  });

  it('does not let an older re-read replace a newer view when two finish out of order', async () => {
    const releases: Record<number, (view: GuestView) => void> = {};
    fake.get.mockImplementation(
      () =>
        new Promise<GuestView>((resolve) => (releases[Object.keys(releases).length + 8] = resolve)),
    );
    await push(audienceState({ seq: 51, revision: 8 }));
    await push(audienceState({ seq: 52, revision: 9 }));
    const at = (revision: number, title: string) => ({
      ...guestView('live'),
      deckTitle: title,
      state: audienceState({ revision }),
    });

    await act(async () => releases[9](at(9, 'Newer')));
    await act(async () => releases[8](at(8, 'Older')));

    expect(latest.view.deckTitle).toBe('Newer');
  });

  it('updates nothing after unmount and stops the connection', async () => {
    await act(() => root.unmount());
    expect(fake.connection.stop).toHaveBeenCalled();
    const before = seen.current;
    await act(async () => fake.handlers.audienceEvent(audienceState({ seq: 99 })));
    await act(async () => fake.handlers.close());
    expect(seen.current).toBe(before);
    root = createRoot(document.createElement('div'));
  });

  it('reconnects for a new code', async () => {
    fake.connection.invoke.mockClear();
    const view = guestView('live');
    function Other() {
      const audience = useAudience('111111', view, () => 0);
      useEffect(() => report(audience));
      return null;
    }
    await act(() => root.unmount());
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Other)));
    await flush();
    expect(fake.connection.invoke).toHaveBeenCalledExactlyOnceWith('JoinAudience', '111111');
  });
});

// SW-883 K5: the phone's language. The first join is JoinAudience, then SetLanguage when the phone wants another language
// than the primary (en in the sample), and both again after a reconnect. Every re-read carries ?language=.
describe('useAudience language', () => {
  const thai = audienceState({ seq: 51, language: { code: 'th', fallbackLines: 2 } });
  const english = audienceState({ seq: 50, language: { code: 'en', fallbackLines: 0 } });
  const mountIn = async (language: string | null) => {
    await act(async () => root.unmount());
    start.current = language;
    fake.connection.invoke.mockReset();
    fake.connection.invoke.mockImplementation(async (method: string) =>
      method === 'SetLanguage' ? thai : english,
    );
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();
  };

  it('calls SetLanguage after JoinAudience and lands on its answer', async () => {
    await mountIn('th');

    expect(fake.connection.invoke.mock.calls).toEqual([
      ['JoinAudience', '482913'],
      ['SetLanguage', '482913', 'th'],
    ]);
    expect(latest.state.language?.code).toBe('th');
    expect(latest.status).toBe('live');
  });

  it('holds the first render until SetLanguage answers', async () => {
    let answer: (state: AudienceState) => void = () => undefined;
    await act(async () => root.unmount());
    start.current = 'th';
    fake.connection.invoke.mockReset();
    fake.connection.invoke.mockImplementation((method: string) =>
      method === 'SetLanguage' ? new Promise<AudienceState>((done) => (answer = done)) : english,
    );
    root = createRoot(document.createElement('div'));
    await act(async () => root.render(createElement(Probe, { report })));
    await flush();
    expect(latest.status).toBe('connecting');

    await act(async () => answer(thai));
    expect(latest.status).toBe('live');
    expect(latest.state.language?.code).toBe('th');
  });

  it('does not call SetLanguage for the primary language', async () => {
    await mountIn('en');

    expect(fake.connection.invoke).toHaveBeenCalledExactlyOnceWith('JoinAudience', '482913');
  });

  it('calls both again after a reconnect', async () => {
    await mountIn('th');
    fake.connection.invoke.mockClear();

    await act(async () => fake.handlers.reconnected());
    await flush();

    expect(fake.connection.invoke.mock.calls).toEqual([
      ['JoinAudience', '482913'],
      ['SetLanguage', '482913', 'th'],
    ]);
  });

  it('lands on the join state and forgets the language when SetLanguage is refused', async () => {
    await mountIn('th');
    fake.connection.invoke.mockReset();
    fake.connection.invoke.mockImplementation(async (method: string) => {
      if (method === 'SetLanguage') throw hubRefusal('not_joined');
      return english;
    });

    await act(async () => fake.handlers.reconnected());
    await flush();

    expect(latest.status).toBe('live');
    expect(latest.state.language?.code).toBe('en');
  });

  it('setLanguage invokes SetLanguage, lands on the answer and carries the language on re-reads', async () => {
    await mountIn(null);
    fake.connection.invoke.mockResolvedValue(thai);
    fake.get.mockClear();

    await act(async () => latest.setLanguage('th'));
    expect(fake.connection.invoke).toHaveBeenLastCalledWith('SetLanguage', '482913', 'th');
    expect(latest.state.language?.code).toBe('th');

    await push(audienceState({ seq: 52, revision: 9 }));
    expect(fake.get).toHaveBeenLastCalledWith('/api/join/482913?language=th');
  });

  it('setLanguage rejects when the hub refuses and keeps the state', async () => {
    await mountIn(null);
    fake.connection.invoke.mockRejectedValue(hubRefusal('not_joined'));

    await expect(act(async () => latest.setLanguage('th'))).rejects.toThrow('not_joined');
    expect(latest.state.language?.code).toBe('en');
  });

  it('re-reads without a language query while the phone has none', async () => {
    await mountIn(null);
    fake.get.mockClear();

    await push(audienceState({ seq: 52, revision: 9 }));
    expect(fake.get).toHaveBeenLastCalledWith('/api/join/482913');
  });
});
