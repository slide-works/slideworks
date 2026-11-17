import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import type { AudiencePolls, AudienceStatus } from '../../guest/useAudience';
import { guestPoll, pollActivity, pollTally } from '../../test-support/fixtures/pollStates';
import type { AnswerRequest, GuestPollView, PollActivity, PollTally } from '../types';
import { PollAnswerLive } from './PollAnswerLive';
import { usePollAnswer, type PollAnswer } from './usePollAnswer';
import type { AnswerOutcome } from './pollApi';

const api = vi.hoisted(() => ({ getGuestPoll: vi.fn(), putPollAnswer: vi.fn() }));
vi.mock('./pollApi', () => api);

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const CODE = '482913';
const ROUND = '0b7c6a52-1f0e-4f43-9d3a-5a1c2e8b7d10';
const NOT_ANSWERED = new Error(
  "Failed to invoke 'WatchPoll' due to an error on the server. HubException: not_answered",
);

// A call the test answers when it chooses.
type Pending<T> = { resolve(value: T): void; reject(error: unknown): void };
function deferred<T>() {
  let pending!: Pending<T>;
  const promise = new Promise<T>((resolve, reject) => (pending = { resolve, reject }));
  return { promise, ...pending };
}

// The audience connection's poll channel, driven by hand: push hands a tally to every subscriber.
function fakePolls() {
  const listeners = new Set<(tally: PollTally) => void>();
  const order: string[] = [];
  const polls: AudiencePolls = {
    onTally: (listener) => {
      order.push('onTally');
      listeners.add(listener);
      return () => void listeners.delete(listener);
    },
    watchPoll: vi.fn(() => {
      order.push('watchPoll');
      return Promise.resolve();
    }),
  };
  return {
    polls,
    order,
    get subscribers() {
      return listeners.size;
    },
    push: (tally: PollTally) => act(async () => listeners.forEach((listener) => listener(tally))),
  };
}

let root: Root;
let channel: ReturnType<typeof fakePolls>;
const seen: { answers: PollAnswer[] } = { answers: [] };
const latest = () => seen.answers[seen.answers.length - 1];

function Probe() {
  const answer = usePollAnswer();
  useEffect(() => {
    seen.answers.push(answer);
  });
  return null;
}

const view = (overrides: Partial<GuestPollView> = {}, activity?: Partial<PollActivity>) => {
  const base = guestPoll('notAnswered');
  return {
    ...base,
    tally: null,
    ...overrides,
    activity: { ...base.activity!, ...activity },
  } as GuestPollView;
};

const pickOne = (overrides: Partial<PollActivity> = {}) =>
  pollActivity('open', {
    pick: 'one',
    max: null,
    results: 'live',
    roomSeesCounts: true,
    ...overrides,
  });
const pickSeveral = (overrides: Partial<PollActivity> = {}) =>
  pollActivity('open', {
    pick: 'upTo',
    max: 2,
    results: 'live',
    roomSeesCounts: true,
    ...overrides,
  });

const accepted = (picks: number[], revision: number): AnswerOutcome => ({
  type: 'accepted',
  answer: { picks, revision },
});

// Answers each PUT as the server would for a guest with no other client: revision + 1.
const acceptEveryPut = () =>
  api.putPollAnswer.mockImplementation(async (_code: string, body: AnswerRequest) =>
    accepted(
      [...body.picks].sort((a, b) => a - b),
      body.revision + 1,
    ),
  );

const render = (activity: PollActivity, status: AudienceStatus = 'live') =>
  act(async () =>
    root.render(
      createElement(PollAnswerLive, {
        code: CODE,
        activity,
        status,
        polls: channel.polls,
        children: createElement(Probe),
      }),
    ),
  );

const settle = () => act(async () => undefined);
const puts = () => api.putPollAnswer.mock.calls.map((call) => call[1] as AnswerRequest);

beforeEach(() => {
  vi.clearAllMocks();
  seen.answers = [];
  channel = fakePolls();
  api.getGuestPoll.mockResolvedValue(view());
  acceptEveryPut();
  root = createRoot(document.createElement('div'));
});

afterEach(async () => {
  await act(() => root.unmount());
});

describe('PollAnswerLive reads', () => {
  it('is loading from mount until the first read lands, then holds its mine, tally and joinedLate', async () => {
    const read = deferred<GuestPollView>();
    api.getGuestPoll.mockReturnValueOnce(read.promise);
    const activity = pickOne();

    await render(activity);
    expect(latest().state).toBe('loading');
    expect(api.getGuestPoll).toHaveBeenCalledWith(CODE);

    await act(async () =>
      read.resolve(
        view(
          {
            mine: { picks: [2], revision: 3 },
            tally: pollTally({ round: ROUND, answered: 9, counts: [1, 2, 5, 1] }),
          },
          activity,
        ),
      ),
    );

    expect(latest()).toMatchObject({
      state: 'idle',
      mine: { picks: [2], revision: 3 },
      picks: [2],
      tally: { round: ROUND, answered: 9, counts: [1, 2, 5, 1] },
      joinedLate: false,
      error: null,
    });
  });

  it('subscribes to tallies before the first read', async () => {
    const order: string[] = channel.order;
    api.getGuestPoll.mockImplementation(async () => {
      order.push('get');
      return view();
    });

    await render(pickOne());

    expect(order).toEqual(['onTally', 'get']);
  });

  it('reads again when the round, the phase or roomSeesCounts changes, and not for any other change', async () => {
    const activity = pickOne({ roomSeesCounts: false, results: 'afterClose' });
    await render(activity);
    expect(api.getGuestPoll).toHaveBeenCalledTimes(1);

    await render({ ...activity, question: 'Same poll, new object' });
    expect(api.getGuestPoll).toHaveBeenCalledTimes(1);

    await render({ ...activity, roomSeesCounts: true });
    expect(api.getGuestPoll).toHaveBeenCalledTimes(2);

    await render({ ...activity, roomSeesCounts: true, phase: 'closed' });
    expect(api.getGuestPoll).toHaveBeenCalledTimes(3);

    await render({ ...activity, round: 'round-2', number: 2 });
    expect(api.getGuestPoll).toHaveBeenCalledTimes(4);
  });

  it('a new round starts with no picks, no answer and no tally until its read lands', async () => {
    const activity = pickOne();
    api.getGuestPoll.mockResolvedValueOnce(
      view(
        {
          mine: { picks: [1], revision: 1 },
          tally: pollTally({ round: ROUND, counts: [1, 1, 0, 0] }),
        },
        activity,
      ),
    );
    await render(activity);
    expect(latest().mine).not.toBeNull();
    const next = deferred<GuestPollView>();
    api.getGuestPoll.mockReturnValueOnce(next.promise);

    await render({ ...activity, round: 'round-2', number: 2 });

    expect(latest()).toMatchObject({ picks: [], mine: null, tally: null, joinedLate: false });
  });

  it('reads again after a reconnect and is loading until that read lands', async () => {
    const activity = pickOne();
    await render(activity);
    await render(activity, 'reconnecting');
    expect(api.getGuestPoll).toHaveBeenCalledTimes(1);
    const reread = deferred<GuestPollView>();
    api.getGuestPoll.mockReturnValueOnce(reread.promise);

    await render(activity, 'live');

    expect(api.getGuestPoll).toHaveBeenCalledTimes(2);
    expect(latest().state).toBe('loading');
    await act(async () => reread.resolve(view({ mine: { picks: [3], revision: 2 } }, activity)));
    expect(latest()).toMatchObject({ state: 'idle', mine: { picks: [3], revision: 2 } });
  });

  it('applies only the newest read when two are out', async () => {
    const first = deferred<GuestPollView>();
    const second = deferred<GuestPollView>();
    api.getGuestPoll.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);
    const activity = pickOne({ roomSeesCounts: false });
    await render(activity);
    await render({ ...activity, roomSeesCounts: true });

    await act(async () => second.resolve(view({ mine: { picks: [1], revision: 2 } }, activity)));
    await act(async () => first.resolve(view({ mine: { picks: [0], revision: 1 } }, activity)));

    expect(latest().mine).toEqual({ picks: [1], revision: 2 });
  });

  it('a failed read is failed with the body’s error, or a message of its own when it never reached the server', async () => {
    api.getGuestPoll.mockRejectedValueOnce(new ApiError('too_many_attempts', 429));
    const activity = pickOne({ roomSeesCounts: false });
    await render(activity);
    expect(latest()).toMatchObject({ state: 'failed', error: 'too_many_attempts' });

    api.getGuestPoll.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    await render({ ...activity, roomSeesCounts: true });
    expect(latest()).toMatchObject({
      state: 'failed',
      error: 'Couldn’t load the poll. Check your connection.',
    });
  });
});

describe('PollAnswerLive pick one', () => {
  it('toggle sends at once with the current revision, and a second toggle replaces it with the next', async () => {
    const activity = pickOne();
    await render(activity);

    await act(async () => latest().toggle(2));
    expect(puts()).toEqual([{ round: ROUND, picks: [2], revision: 0 }]);
    expect(latest()).toMatchObject({
      state: 'idle',
      picks: [2],
      mine: { picks: [2], revision: 1 },
    });

    await act(async () => latest().toggle(0));
    expect(puts()).toEqual([
      { round: ROUND, picks: [2], revision: 0 },
      { round: ROUND, picks: [0], revision: 1 },
    ]);
    expect(latest()).toMatchObject({
      state: 'idle',
      picks: [0],
      mine: { picks: [0], revision: 2 },
    });
  });

  it('is sending while the PUT is out, and a tap meanwhile goes after it with the revision it returns', async () => {
    const out = deferred<AnswerOutcome>();
    api.putPollAnswer.mockReturnValueOnce(out.promise);
    await render(pickOne());

    await act(async () => latest().toggle(1));
    expect(latest()).toMatchObject({ state: 'sending', picks: [1] });
    await act(async () => latest().toggle(3));
    expect(puts()).toHaveLength(1);
    expect(latest().picks).toEqual([3]);

    await act(async () => out.resolve(accepted([1], 1)));

    expect(puts()).toEqual([
      { round: ROUND, picks: [1], revision: 0 },
      { round: ROUND, picks: [3], revision: 1 },
    ]);
    expect(latest()).toMatchObject({
      state: 'idle',
      picks: [3],
      mine: { picks: [3], revision: 2 },
    });
  });

  it('nothing is sent while loading or when the poll is not open', async () => {
    const read = deferred<GuestPollView>();
    api.getGuestPoll.mockReturnValueOnce(read.promise);
    const activity = pickOne();
    await render(activity);
    await act(async () => latest().toggle(1));
    await act(async () => read.resolve(view({}, activity)));

    await render({ ...activity, phase: 'closed' });
    await act(async () => latest().toggle(1));
    await act(async () => latest().send());

    expect(api.putPollAnswer).not.toHaveBeenCalled();
    expect(latest().picks).toEqual([]);
  });
});

describe('PollAnswerLive pick several', () => {
  it('toggle only selects, up to max, and send sends once', async () => {
    await render(pickSeveral());

    await act(async () => latest().toggle(3));
    await act(async () => latest().toggle(0));
    await act(async () => latest().toggle(1));
    expect(latest().picks).toEqual([0, 3]);
    await act(async () => latest().toggle(3));
    await act(async () => latest().toggle(1));
    expect(latest().picks).toEqual([0, 1]);
    expect(api.putPollAnswer).not.toHaveBeenCalled();

    await act(async () => latest().send());

    expect(puts()).toEqual([{ round: ROUND, picks: [0, 1], revision: 0 }]);
    expect(latest()).toMatchObject({
      state: 'idle',
      picks: [0, 1],
      mine: { picks: [0, 1], revision: 1 },
    });
  });

  it('a read that lands while picking keeps the picks on the phone', async () => {
    const activity = pickSeveral({ roomSeesCounts: false });
    await render(activity);
    await act(async () => latest().toggle(2));

    await render({ ...activity, roomSeesCounts: true });

    expect(latest().picks).toEqual([2]);
  });
});

describe('PollAnswerLive conflicts and failures', () => {
  it('a 409 keeps the picks and is conflict; send then uses the revision from current, and clear empties the picks', async () => {
    const activity = pickSeveral();
    await render(activity);
    await act(async () => latest().toggle(1));
    await act(async () => latest().toggle(2));
    api.putPollAnswer.mockResolvedValueOnce({
      type: 'stale',
      current: view({ mine: { picks: [0], revision: 4 } }, activity),
    } satisfies AnswerOutcome);

    await act(async () => latest().send());
    expect(latest()).toMatchObject({
      state: 'conflict',
      picks: [1, 2],
      mine: { picks: [0], revision: 4 },
    });

    await act(async () => latest().send());
    expect(puts()[1]).toEqual({ round: ROUND, picks: [1, 2], revision: 4 });
    expect(latest()).toMatchObject({ state: 'idle', mine: { picks: [1, 2], revision: 5 } });

    api.putPollAnswer.mockResolvedValueOnce({
      type: 'stale',
      current: view({ mine: { picks: [3], revision: 6 } }, activity),
    } satisfies AnswerOutcome);
    await act(async () => latest().toggle(1));
    await act(async () => latest().send());
    expect(latest().state).toBe('conflict');

    await act(async () => latest().clear());

    expect(latest()).toMatchObject({ state: 'idle', picks: [], mine: { picks: [3], revision: 6 } });
    expect(api.putPollAnswer).toHaveBeenCalledTimes(3);
  });

  it('a refusal that is not a 409 is failed with the body’s error', async () => {
    api.putPollAnswer.mockRejectedValueOnce(new ApiError('too_many_answers', 429));
    await render(pickOne());

    await act(async () => latest().toggle(1));

    expect(latest()).toMatchObject({
      state: 'failed',
      error: 'too_many_answers',
      picks: [1],
      mine: null,
    });
  });

  it('a 400 with no error field is failed with a generic message', async () => {
    api.putPollAnswer.mockRejectedValueOnce(new ApiError('Request failed (400)', 400));
    await render(pickOne());

    await act(async () => latest().toggle(1));

    expect(latest()).toMatchObject({ state: 'failed', error: 'Request failed (400)' });
  });

  it('a send that never reached the server while live is failed with a message of its own, and send tries again', async () => {
    api.putPollAnswer.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    await render(pickOne());

    await act(async () => latest().toggle(1));
    expect(latest()).toMatchObject({
      state: 'failed',
      error: 'Your answer wasn’t sent. Check your connection and try again.',
    });

    await act(async () => latest().send());
    expect(puts()[1]).toEqual({ round: ROUND, picks: [1], revision: 0 });
    expect(latest()).toMatchObject({
      state: 'idle',
      error: null,
      mine: { picks: [1], revision: 1 },
    });
  });
});

describe('PollAnswerLive offline', () => {
  it('a pick made while reconnecting is queued, and sent after the re-read when the round is still open', async () => {
    const activity = pickOne();
    await render(activity);
    await render(activity, 'reconnecting');

    await act(async () => latest().toggle(2));
    expect(latest()).toMatchObject({ state: 'queued', picks: [2] });
    expect(api.putPollAnswer).not.toHaveBeenCalled();

    api.getGuestPoll.mockResolvedValueOnce(view({}, activity));
    await render(activity, 'live');

    expect(api.getGuestPoll).toHaveBeenCalledTimes(2);
    expect(puts()).toEqual([{ round: ROUND, picks: [2], revision: 0 }]);
    expect(latest()).toMatchObject({ state: 'idle', mine: { picks: [2], revision: 1 } });
  });

  it('a queued pick is dropped as failed when the round closed while the phone was away', async () => {
    const activity = pickOne();
    await render(activity);
    await render(activity, 'reconnecting');
    await act(async () => latest().toggle(2));

    const closed = { ...activity, phase: 'closed' as const };
    api.getGuestPoll.mockResolvedValueOnce(view({}, closed));
    await render(closed, 'live');

    expect(api.putPollAnswer).not.toHaveBeenCalled();
    expect(latest()).toMatchObject({
      state: 'failed',
      error: 'The poll closed before your answer was sent.',
      picks: [],
    });
  });

  it('a queued pick is dropped as failed when a new round started while the phone was away', async () => {
    const activity = pickOne();
    await render(activity, 'offline');
    await act(async () => latest().toggle(0));
    expect(latest().state).toBe('queued');

    const next = { ...activity, round: 'round-2', number: 2 };
    api.getGuestPoll.mockResolvedValueOnce(view({}, next));
    await render(next, 'live');

    expect(api.putPollAnswer).not.toHaveBeenCalled();
    expect(latest()).toMatchObject({
      state: 'failed',
      error: 'The poll closed before your answer was sent.',
    });
  });

  it('a send that fails because the connection went is queued and goes when it is back', async () => {
    const out = deferred<AnswerOutcome>();
    api.putPollAnswer.mockReturnValueOnce(out.promise);
    const activity = pickOne();
    await render(activity);
    await act(async () => latest().toggle(3));
    await render(activity, 'reconnecting');

    await act(async () => out.reject(new TypeError('Failed to fetch')));
    expect(latest()).toMatchObject({ state: 'queued', picks: [3] });

    await render(activity, 'live');
    expect(puts()).toEqual([
      { round: ROUND, picks: [3], revision: 0 },
      { round: ROUND, picks: [3], revision: 0 },
    ]);
    expect(latest()).toMatchObject({ state: 'idle', mine: { picks: [3], revision: 1 } });
  });
});

describe('PollAnswerLive tallies', () => {
  it('keeps the latest tally for the round; one with null counts never clears the counts held for it', async () => {
    await render(pickOne());

    await channel.push(pollTally({ round: ROUND, answered: 4, counts: [1, 1, 1, 1] }));
    await channel.push(pollTally({ round: ROUND, answered: 5, counts: null }));
    expect(latest().tally).toEqual({
      type: 'pollTally',
      round: ROUND,
      answered: 5,
      counts: [1, 1, 1, 1],
    });

    await channel.push(pollTally({ round: ROUND, answered: 6, counts: [2, 1, 2, 1] }));
    expect(latest().tally).toEqual({
      type: 'pollTally',
      round: ROUND,
      answered: 6,
      counts: [2, 1, 2, 1],
    });
  });

  it('a tally with null counts and no counts held stays null, and is never made up', async () => {
    await render(pickOne({ roomSeesCounts: false, results: 'afterClose' }));

    await channel.push(pollTally({ round: ROUND, answered: 7, counts: null }));

    expect(latest().tally).toEqual({ type: 'pollTally', round: ROUND, answered: 7, counts: null });
  });

  it('a tally for another round is dropped', async () => {
    await render(pickOne());

    await channel.push(
      pollTally({ round: 'another-round', answered: 40, counts: [10, 10, 10, 10] }),
    );

    expect(latest().tally).toBeNull();
  });

  it('a read whose tally has null counts keeps the counts held for the round', async () => {
    const activity = pickOne({ roomSeesCounts: true });
    await render(activity);
    await channel.push(pollTally({ round: ROUND, answered: 4, counts: [1, 1, 1, 1] }));
    api.getGuestPoll.mockResolvedValueOnce(
      view({ tally: pollTally({ round: ROUND, answered: 5, counts: null }) }, activity),
    );

    await render({ ...activity, roomSeesCounts: false });

    expect(latest().tally).toEqual({
      type: 'pollTally',
      round: ROUND,
      answered: 5,
      counts: [1, 1, 1, 1],
    });
  });

  it('unsubscribes from tallies when unmounted', async () => {
    await render(pickOne());
    expect(channel.subscribers).toBe(1);

    await act(async () => root.render(null));

    expect(channel.subscribers).toBe(0);
  });
});

describe('PollAnswerLive results group', () => {
  const afterAnswer = (overrides: Partial<PollActivity> = {}) =>
    pickOne({ results: 'afterAnswer', roomSeesCounts: false, ...overrides });

  it('after an answer on an afterAnswer poll it calls WatchPoll once', async () => {
    await render(afterAnswer());
    expect(channel.polls.watchPoll).not.toHaveBeenCalled();

    await act(async () => latest().toggle(1));
    await act(async () => latest().toggle(2));

    expect(channel.polls.watchPoll).toHaveBeenCalledTimes(1);
  });

  it('watches once the first read says this guest has answered', async () => {
    const activity = afterAnswer();
    api.getGuestPoll.mockResolvedValueOnce(view({ mine: { picks: [1], revision: 1 } }, activity));

    await render(activity);

    expect(channel.polls.watchPoll).toHaveBeenCalledTimes(1);
  });

  it('watches again after a reconnect, before the re-read', async () => {
    const activity = afterAnswer();
    await render(activity);
    await act(async () => latest().toggle(1));
    expect(channel.order).toEqual(['onTally', 'watchPoll']);
    await render(activity, 'reconnecting');
    api.getGuestPoll.mockImplementationOnce(async () => {
      channel.order.push('get');
      return view({ mine: { picks: [1], revision: 1 } }, activity);
    });

    await render(activity, 'live');

    expect(channel.order).toEqual(['onTally', 'watchPoll', 'watchPoll', 'get']);
  });

  it('does not watch a live, afterClose or closed poll', async () => {
    await render(pickOne({ results: 'live' }));
    await act(async () => latest().toggle(1));
    await render(pickOne({ results: 'afterClose', roomSeesCounts: false }));
    await act(async () => latest().toggle(2));
    const closed = afterAnswer({ phase: 'closed', roomSeesCounts: true });
    api.getGuestPoll.mockResolvedValueOnce(view({ mine: { picks: [1], revision: 1 } }, closed));
    await render(closed);

    expect(channel.polls.watchPoll).not.toHaveBeenCalled();
  });

  it('a new round needs a new watch', async () => {
    const activity = afterAnswer();
    await render(activity);
    await act(async () => latest().toggle(1));
    expect(channel.polls.watchPoll).toHaveBeenCalledTimes(1);

    await render({ ...activity, round: 'round-2', number: 2 });
    await act(async () => latest().toggle(0));

    expect(channel.polls.watchPoll).toHaveBeenCalledTimes(2);
  });

  it('a not_answered refusal leaves the card as it is and a later answer watches again', async () => {
    const activity = afterAnswer();
    vi.mocked(channel.polls.watchPoll).mockRejectedValueOnce(NOT_ANSWERED);
    api.getGuestPoll.mockResolvedValueOnce(view({ mine: { picks: [1], revision: 1 } }, activity));
    await render(activity);
    await settle();
    expect(latest()).toMatchObject({ state: 'idle', error: null });

    await act(async () => latest().toggle(2));

    expect(channel.polls.watchPoll).toHaveBeenCalledTimes(2);
  });

  it('any other WatchPoll refusal is failed with its code', async () => {
    const activity = afterAnswer();
    vi.mocked(channel.polls.watchPoll).mockRejectedValueOnce(
      new Error(
        "Failed to invoke 'WatchPoll' due to an error on the server. HubException: no_session",
      ),
    );
    await render(activity);

    await act(async () => latest().toggle(2));
    await settle();

    expect(latest()).toMatchObject({ state: 'failed', error: 'no_session' });
  });
});
