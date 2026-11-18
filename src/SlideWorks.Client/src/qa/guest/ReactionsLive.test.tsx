import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { qaRoom } from '../../test-support/fixtures/qaStates';
import type { QaRoom } from '../types';
import { ReactionsLive } from './ReactionsLive';
import { usePhoneReactions, type PhoneReactions } from './usePhoneReactions';

const api = vi.hoisted(() => ({
  getQuestions: vi.fn(),
  postQuestion: vi.fn(),
  putVotes: vi.fn(),
  postReaction: vi.fn(),
}));
vi.mock('./questionsApi', () => api);

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const CODE = '482913';

let root: Root;
const seen: { values: PhoneReactions[] } = { values: [] };
const latest = () => seen.values[seen.values.length - 1];

function Probe() {
  const reactions = usePhoneReactions();
  useEffect(() => {
    seen.values.push(reactions);
  });
  return null;
}

const render = (room: QaRoom | null) =>
  act(async () =>
    root.render(
      createElement(ReactionsLive, {
        code: CODE,
        room,
        status: 'live',
        children: createElement(Probe),
      }),
    ),
  );

const tap = () => act(() => latest().tap('clap'));
const tick = (ms: number) => act(async () => vi.advanceTimersByTimeAsync(ms));

beforeEach(() => {
  vi.clearAllMocks();
  seen.values = [];
  api.postReaction.mockResolvedValue({ type: 'sent' });
  root = createRoot(document.createElement('div'));
});

afterEach(async () => {
  await act(() => root.unmount());
  vi.useRealTimers();
});

describe('ReactionsLive', () => {
  it('is enabled only when the room has reactions on', async () => {
    await render(null);
    expect(latest().enabled).toBe(false);
    await render(qaRoom('offHere'));
    expect(latest().enabled).toBe(false);

    await render(qaRoom('open'));

    expect(latest().enabled).toBe(true);
  });

  it('a tap sends the emoji for the code', async () => {
    await render(qaRoom('open'));

    await tap();

    expect(api.postReaction).toHaveBeenCalledWith(CODE, 'clap');
    expect(latest().error).toBeNull();
  });

  it('a 429 cools the row for the rest of the 5 seconds, sends nothing meanwhile, then cools down', async () => {
    vi.useFakeTimers();
    await render(qaRoom('open'));
    await tap();
    await tick(2000);
    api.postReaction.mockResolvedValueOnce({ type: 'limited' });

    await tap();
    expect(latest().cooling).toBe(true);
    await tap();
    expect(api.postReaction).toHaveBeenCalledTimes(2);

    await tick(2999);
    expect(latest().cooling).toBe(true);
    await tick(1);

    expect(latest().cooling).toBe(false);
    await tap();
    expect(api.postReaction).toHaveBeenCalledTimes(3);
  });

  it('a 409 reactions_off sets error with the code, and the next tap that goes through clears it', async () => {
    api.postReaction.mockResolvedValueOnce({ type: 'off' });
    await render(qaRoom('open'));

    await tap();
    expect(latest().error).toBe('reactions_off');

    await tap();
    expect(latest().error).toBeNull();
  });

  it('a refusal sets error with its code, and a call that never reached the server with the network sentence', async () => {
    api.postReaction.mockRejectedValueOnce(new ApiError('emoji: unknown', 400));
    await render(qaRoom('open'));

    await tap();
    expect(latest().error).toBe('emoji: unknown');

    api.postReaction.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    await tap();
    expect(latest().error).toBe(
      'Your phone couldn’t reach the talk. Check your connection, then try again.',
    );
  });
});
