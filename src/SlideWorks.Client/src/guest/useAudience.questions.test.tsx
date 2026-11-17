import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { pollTally } from '../test-support/fixtures/pollStates';
import {
  qaRoom,
  questionControl,
  questionTally,
  reactionsBurst,
} from '../test-support/fixtures/qaStates';
import type { QuestionTally } from '../qa/types';
import { useAudience, type Audience } from './useAudience';

// A hub connection the test drives by hand, as in useAudience.test.tsx and useAudience.polls.test.tsx.
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

describe('useAudience questions', () => {
  it('hands every questionTally to each subscriber in order, and nothing after unsubscribing', async () => {
    const got: QuestionTally[] = [];
    const also: QuestionTally[] = [];
    const unsubscribe = latest().questions.onTally((tally) => got.push(tally));
    latest().questions.onTally((tally) => also.push(tally));
    const first = questionTally({ listRevision: 7 });
    const second = questionTally({ listRevision: 8 });

    await act(async () => {
      fake.handlers.audienceEvent(first);
      fake.handlers.audienceEvent(second);
    });

    expect(got).toEqual([first, second]);
    expect(also).toEqual([first, second]);
    unsubscribe();
    await event(questionTally());
    expect(got).toHaveLength(2);
    expect(also).toHaveLength(3);
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
    latest().questions.onTally(() => {
      throw thrown;
    });
    latest().questions.onTally(after);

    await event(questionTally());

    expect(after).toHaveBeenCalledOnce();
    expect(rethrown).toEqual([thrown]);
    microtask.mockRestore();
  });

  it('keeps questionTally and pollTally apart', async () => {
    const questions = vi.fn();
    const polls = vi.fn();
    latest().questions.onTally(questions);
    latest().polls.onTally(polls);

    await event(pollTally());
    await event(questionTally());

    expect(polls).toHaveBeenCalledOnce();
    expect(questions).toHaveBeenCalledOnce();
  });

  it('gives questionControl and reactions to nobody and leaves the state alone', async () => {
    const listener = vi.fn();
    latest().questions.onTally(listener);
    const before = latest().state;

    await event(questionControl());
    await event(reactionsBurst());

    expect(listener).not.toHaveBeenCalled();
    expect(latest().state).toBe(before);
  });

  it('puts the room of a newer audienceState on the state', async () => {
    const room = qaRoom('closed');

    await event(audienceState({ seq: 60, room }));

    expect(latest().state.room).toEqual(room);
  });

  it('keeps one questions object across renders', async () => {
    const first = latest().questions;

    await event(audienceState({ seq: 60 }));

    expect(seen.audiences.length).toBeGreaterThan(1);
    expect(latest().questions).toBe(first);
  });
});
