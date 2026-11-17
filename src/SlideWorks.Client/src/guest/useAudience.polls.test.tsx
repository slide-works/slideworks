import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { pollControl, pollTally } from '../test-support/fixtures/pollStates';
import type { PollTally } from '../polls/types';
import { useAudience, type Audience } from './useAudience';

// A hub connection the test drives by hand, as in useAudience.test.tsx.
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
  return { connection, handlers };
});

vi.mock('../realtime/audienceConnection', () => ({
  createAudienceConnection: () => fake.connection,
}));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
const seen: { audiences: Audience[] } = { audiences: [] };
const latest = () => seen.audiences[seen.audiences.length - 1];
const initial = guestView('live');

function Probe() {
  const audience = useAudience('482913', initial, () => 0);
  useEffect(() => {
    seen.audiences.push(audience);
  });
  return null;
}

const event = (value: unknown) => act(async () => void fake.handlers.audienceEvent(value));

beforeEach(async () => {
  vi.clearAllMocks();
  seen.audiences = [];
  fake.connection.state = 'Connected';
  fake.connection.invoke.mockResolvedValue(audienceState({ seq: 50 }));
  root = createRoot(document.createElement('div'));
  await act(async () => root.render(createElement(Probe)));
});

afterEach(async () => {
  await act(() => root.unmount());
});

describe('useAudience polls', () => {
  it('hands every pollTally to each subscriber in order, two in one tick included', async () => {
    const got: PollTally[] = [];
    const unsubscribe = latest().polls.onTally((tally) => got.push(tally));
    const hidden = pollTally({ counts: null });
    const shown = pollTally({ counts: [1, 2, 3, 4] });

    await act(async () => {
      fake.handlers.audienceEvent(hidden);
      fake.handlers.audienceEvent(shown);
    });

    expect(got).toEqual([hidden, shown]);
    unsubscribe();
    await event(pollTally());
    expect(got).toHaveLength(2);
  });

  it('a listener that throws does not stop the others, and its error is thrown again on its own', async () => {
    const thrown = new Error('listener broke');
    const rethrown: unknown[] = [];
    const microtask = vi.spyOn(globalThis, 'queueMicrotask').mockImplementation((run) => {
      try {
        run();
      } catch (error) {
        rethrown.push(error);
      }
    });
    const after = vi.fn();
    latest().polls.onTally(() => {
      throw thrown;
    });
    latest().polls.onTally(after);

    await event(pollTally());

    expect(after).toHaveBeenCalledOnce();
    expect(rethrown).toEqual([thrown]);
    microtask.mockRestore();
  });

  it('gives pollControl to nobody and leaves the state alone', async () => {
    const listener = vi.fn();
    latest().polls.onTally(listener);
    const before = latest().state;

    await event(pollControl());

    expect(listener).not.toHaveBeenCalled();
    expect(latest().state).toBe(before);
  });

  it('keeps one polls object across renders', async () => {
    const first = latest().polls;

    await event(audienceState({ seq: 60 }));

    expect(seen.audiences.length).toBeGreaterThan(1);
    expect(latest().polls).toBe(first);
  });

  it('watchPoll invokes WatchPoll with the code on the audience connection', async () => {
    fake.connection.invoke.mockResolvedValueOnce(undefined);

    await act(() => latest().polls.watchPoll());

    expect(fake.connection.invoke).toHaveBeenLastCalledWith('WatchPoll', '482913');
  });

  it('watchPoll passes on the hub refusal', async () => {
    const refusal = new Error(
      "An unexpected error occurred invoking 'WatchPoll' on the server. HubException: not_answered",
    );
    fake.connection.invoke.mockRejectedValueOnce(refusal);

    await expect(latest().polls.watchPoll()).rejects.toBe(refusal);
  });

  it('watchPoll rejects once the hook has stopped', async () => {
    const polls = latest().polls;
    await act(() => root.unmount());
    root = createRoot(document.createElement('div'));

    await expect(polls.watchPoll()).rejects.toThrow('The audience connection is closed.');
  });
});
