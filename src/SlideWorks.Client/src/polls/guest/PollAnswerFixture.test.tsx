import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PollAnswerFixture, type PollAnswerFixtureProps } from './PollAnswerFixture';
import { usePollAnswer, type PollAnswer, type PollAnswerState } from './usePollAnswer';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
// The answer the probe last rendered with, kept by an effect so rendering stays pure.
const probe: { answer: PollAnswer | null } = { answer: null };

function Probe() {
  const answer = usePollAnswer();
  useEffect(() => {
    probe.answer = answer;
  });
  return null;
}

const seen = () => probe.answer!;

const render = (props: Omit<PollAnswerFixtureProps, 'children'>) =>
  act(async () =>
    root.render(createElement(PollAnswerFixture, { ...props, children: createElement(Probe) })),
  );

beforeEach(() => {
  probe.answer = null;
  root = createRoot(document.createElement('div'));
});

afterEach(async () => {
  await act(() => root.unmount());
});

describe('PollAnswerFixture', () => {
  it.each<PollAnswerState>(['loading', 'idle', 'sending', 'queued', 'conflict', 'failed'])(
    'reaches the %s state',
    async (state) => {
      await render({ state, error: state === 'failed' ? 'too_many_answers' : null });

      expect(seen().state).toBe(state);
      expect(seen().error).toBe(state === 'failed' ? 'too_many_answers' : null);
    },
  );

  it('gives the answer, the tally and the picks it is handed', async () => {
    const mine = { picks: [1], revision: 2 };
    const tally = { type: 'pollTally' as const, round: 'r1', answered: 3, counts: null };

    await render({ picks: [1], mine, tally });

    expect(seen()).toMatchObject({ picks: [1], mine, tally, state: 'idle' });
  });

  it('pick one: toggle replaces the pick, reports it and sends, as the real hook does', async () => {
    const onToggle = vi.fn();
    const onSend = vi.fn();
    await render({ picks: [0], onToggle, onSend });

    await act(async () => seen().toggle(2));

    expect(seen().picks).toEqual([2]);
    expect(onToggle).toHaveBeenCalledWith(2);
    expect(onSend).toHaveBeenCalledOnce();
  });

  it('pick several: a toggle sends nothing', async () => {
    const onSend = vi.fn();
    await render({ pick: 'upTo', max: 2, onSend });

    await act(async () => seen().toggle(1));

    expect(onSend).not.toHaveBeenCalled();
  });

  it('passes joinedLate on, false by default', async () => {
    await render({});
    expect(seen().joinedLate).toBe(false);

    await render({ joinedLate: true });

    expect(seen().joinedLate).toBe(true);
  });

  it('pick several: toggle adds and removes, never past max', async () => {
    await render({ pick: 'upTo', max: 2, picks: [3] });

    await act(async () => seen().toggle(1));
    await act(async () => seen().toggle(0));
    expect(seen().picks).toEqual([1, 3]);

    await act(async () => seen().toggle(3));
    expect(seen().picks).toEqual([1]);
  });

  it('clear empties the picks and send changes nothing, each reported', async () => {
    const onSend = vi.fn();
    const onClear = vi.fn();
    await render({ picks: [1], onSend, onClear });

    await act(async () => seen().send());
    expect(seen().picks).toEqual([1]);
    await act(async () => seen().clear());

    expect(seen().picks).toEqual([]);
    expect(onSend).toHaveBeenCalledOnce();
    expect(onClear).toHaveBeenCalledOnce();
  });

  it('a new picks prop replaces the held picks', async () => {
    await render({ picks: [0] });
    await act(async () => seen().toggle(2));

    await render({ picks: [3] });

    expect(seen().picks).toEqual([3]);
  });
});

describe('usePollAnswer', () => {
  it('throws outside a provider', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    await expect(act(async () => root.render(createElement(Probe)))).rejects.toThrow(
      'usePollAnswer needs a PollAnswerLive or a PollAnswerFixture above it.',
    );
    error.mockRestore();
  });
});
