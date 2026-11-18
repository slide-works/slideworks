import { Activity, act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import { ApiError } from '../../api/client';
import {
  JITTER_MS,
  REREAD_RETRY_MS,
  SLOW_JITTER_MS,
  THROTTLED_RETRY_MS,
  type AudienceQuestions,
  type AudienceStatus,
} from '../../guest/useAudience';
import { guestQuestions, qaRoom } from '../../test-support/fixtures/qaStates';
import type { GuestQuestionsView, QaRoom, QuestionTally, VoteRequest } from '../types';
import { AudienceQuestionsContext } from './AudienceQuestionsContext';
import { QuestionsLive } from './QuestionsLive';
import type { AskOutcome, VoteOutcome } from './questionsApi';
import { useQuestions, type Questions } from './useQuestions';

const api = vi.hoisted(() => ({
  getQuestions: vi.fn(),
  postQuestion: vi.fn(),
  putVotes: vi.fn(),
  postReaction: vi.fn(),
}));
vi.mock('./questionsApi', () => api);

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const CODE = '482913';
const OTHER_ROUND = '9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d';

// The audience connection's questions channel, driven by hand: push hands a tally to every subscriber.
function fakeChannel() {
  const listeners = new Set<(tally: QuestionTally) => void>();
  const channel: AudienceQuestions = {
    onTally: (listener) => {
      listeners.add(listener);
      return () => void listeners.delete(listener);
    },
  };
  return {
    channel,
    push: (tally: QuestionTally) =>
      act(async () => listeners.forEach((listener) => listener(tally))),
  };
}

let root: Root;
let hub: ReturnType<typeof fakeChannel>;
const seen: { values: Questions[] } = { values: [] };
const latest = () => seen.values[seen.values.length - 1];

function Probe() {
  const questions = useQuestions();
  useEffect(() => {
    seen.values.push(questions);
  });
  return null;
}

const view = guestQuestions('view');
const ROUND = view.room.round!;

// mode 'hidden' hides the provider in an <Activity>, which runs its effects' cleanups; 'visible' runs them again.
const render = (
  room: QaRoom | null,
  status: AudienceStatus = 'live',
  showing = false,
  mode: 'visible' | 'hidden' = 'visible',
) =>
  act(async () =>
    root.render(
      createElement(Activity, {
        mode,
        children: createElement(AudienceQuestionsContext, {
          value: hub.channel,
          children: createElement(QuestionsLive, {
            code: CODE,
            room,
            status,
            showing,
            children: createElement(Probe),
          }),
        }),
      }),
    ),
  );

const settle = () => act(async () => undefined);
const tick = (ms: number) => act(async () => vi.advanceTimersByTimeAsync(ms));
const votes = () => api.putVotes.mock.calls.map((call) => call[1] as VoteRequest);
const votesOf = (n: number) => latest().questions.find((q) => q.number === n)?.votes;

// Answers each PUT as the server would for a guest with no other client: revision + 1.
const acceptEveryPut = () =>
  api.putVotes.mockImplementation(
    async (_code: string, body: VoteRequest): Promise<VoteOutcome> => ({
      type: 'accepted',
      mine: { picks: [...body.picks].sort((a, b) => a - b), revision: body.revision + 1 },
    }),
  );

beforeEach(() => {
  vi.clearAllMocks();
  seen.values = [];
  hub = fakeChannel();
  api.getQuestions.mockResolvedValue(guestQuestions('view'));
  acceptEveryPut();
  root = createRoot(document.createElement('div'));
});

afterEach(async () => {
  await act(() => root.unmount());
  vi.useRealTimers();
});

describe('QuestionsLive reads', () => {
  it('is loading until the first read lands, then ready with the list, mine and the room', async () => {
    let land!: (value: GuestQuestionsView) => void;
    api.getQuestions.mockReturnValueOnce(new Promise((resolve) => (land = resolve)));

    await render(view.room);
    expect(latest().state).toBe('loading');
    expect(latest().room).toBeNull();

    await act(async () => land(guestQuestions('view')));

    expect(latest().state).toBe('ready');
    expect(latest().questions.map((q) => q.number)).toEqual([1, 2, 3, 4, 6]);
    expect(latest().mine).toEqual(view.mine);
    expect(latest().room).toEqual(view.room);
    expect(api.getQuestions).toHaveBeenCalledWith(CODE);
  });

  it('does not read while the room is null or its questions are off', async () => {
    await render(null);
    await render(qaRoom('off'));

    expect(api.getQuestions).not.toHaveBeenCalled();
    expect(latest().state).toBe('loading');
  });

  it('reads again when a tally carries a higher listRevision, and does not apply its votes', async () => {
    await render(view.room);
    expect(api.getQuestions).toHaveBeenCalledTimes(1);

    await hub.push({
      type: 'questionTally',
      round: ROUND,
      listRevision: view.room.listRevision + 1,
      votes: [{ number: 1, votes: 99 }],
    });

    expect(api.getQuestions).toHaveBeenCalledTimes(2);
    expect(votesOf(1)).toBe(14);
  });

  it('reads again when the room carries a higher listRevision, and not when the same room comes again', async () => {
    await render(view.room);
    await render({ ...view.room });
    expect(api.getQuestions).toHaveBeenCalledTimes(1);

    await render({ ...view.room, listRevision: view.room.listRevision + 1 });

    expect(api.getQuestions).toHaveBeenCalledTimes(2);
  });

  it('applies a tally of the same listRevision for its round', async () => {
    await render(view.room);

    await hub.push({
      type: 'questionTally',
      round: ROUND,
      listRevision: view.room.listRevision,
      votes: [{ number: 2, votes: 20 }],
    });

    expect(votesOf(2)).toBe(20);
    expect(api.getQuestions).toHaveBeenCalledTimes(1);
  });

  it('ignores a tally for another round', async () => {
    await render(view.room);

    await hub.push({
      type: 'questionTally',
      round: OTHER_ROUND,
      listRevision: view.room.listRevision + 5,
      votes: [{ number: 2, votes: 20 }],
    });

    expect(votesOf(2)).toBe(9);
    expect(api.getQuestions).toHaveBeenCalledTimes(1);
  });

  it('reads again after a reconnect', async () => {
    await render(view.room, 'live');
    await render(view.room, 'reconnecting');
    expect(api.getQuestions).toHaveBeenCalledTimes(1);

    await render(view.room, 'live');

    expect(api.getQuestions).toHaveBeenCalledTimes(2);
  });

  it('reads again once a minute while the Questions tab is showing, and not while it is hidden', async () => {
    vi.useFakeTimers();
    await render(view.room, 'live', true);
    await tick(60_000);
    expect(api.getQuestions).toHaveBeenCalledTimes(2);

    await render(view.room, 'live', false);
    await tick(120_000);

    expect(api.getQuestions).toHaveBeenCalledTimes(2);
  });

  it('a first read that fails is failed with its code, and retry reads again', async () => {
    api.getQuestions.mockRejectedValueOnce(new ApiError('no_session', 404));
    await render(view.room);
    expect(latest().state).toBe('failed');
    expect(latest().error).toBe('no_session');

    await act(async () => latest().retry());

    expect(latest().state).toBe('ready');
    expect(latest().error).toBeNull();
  });
});

describe('QuestionsLive asks', () => {
  const asked: AskOutcome = {
    type: 'asked',
    asked: {
      question: {
        number: 7,
        text: 'Why?',
        name: null,
        age: 'now',
        state: 'open',
        votes: 1,
        onScreen: false,
        yours: true,
      },
      mine: { picks: [1, 4, 7], revision: 4 },
    },
  };

  it('adds the question and mine on a 201', async () => {
    api.postQuestion.mockResolvedValueOnce(asked);
    await render(view.room);

    await act(() => latest().ask('Why?', null));

    expect(api.postQuestion).toHaveBeenCalledWith(CODE, { text: 'Why?', name: null });
    expect(latest().asking).toBe('idle');
    expect(latest().questions.map((q) => q.number)).toEqual([1, 2, 3, 4, 6, 7]);
    expect(latest().mine).toEqual({ picks: [1, 4, 7], revision: 4 });
  });

  it('a question asked while reconnecting is queued, and sent once after the reconnect', async () => {
    api.postQuestion.mockResolvedValue(asked);
    await render(view.room, 'reconnecting');

    await act(() => latest().ask('Why?', null));
    expect(latest().asking).toBe('queued');
    expect(api.postQuestion).not.toHaveBeenCalled();

    await render(view.room, 'live');
    await render(view.room, 'live');
    await settle();

    expect(api.postQuestion).toHaveBeenCalledTimes(1);
    expect(latest().asking).toBe('idle');
  });

  it('a queued question is not sent when the room has closed meanwhile', async () => {
    await render(view.room, 'reconnecting');
    await act(() => latest().ask('Why?', null));
    api.getQuestions.mockResolvedValue({ ...guestQuestions('view'), room: qaRoom('closed') });

    await render(qaRoom('closed'), 'live');

    expect(api.postQuestion).not.toHaveBeenCalled();
    expect(latest().asking).toBe('failed');
    expect(latest().error).toBe('questions_closed');
  });

  it('a 409 is failed with its code', async () => {
    api.postQuestion.mockResolvedValueOnce({ type: 'refused', error: 'questions_closed' });
    await render(view.room);

    await act(() => latest().ask('Why?', null));

    expect(latest().asking).toBe('failed');
    expect(latest().error).toBe('questions_closed');
  });

  it('a 429 is limited, and retryIn counts down to 0 by whole seconds and returns to idle', async () => {
    vi.useFakeTimers();
    api.postQuestion.mockResolvedValueOnce({ type: 'limited', retryAfter: 3 });
    await render(view.room);

    await act(() => latest().ask('Why?', null));
    expect(latest().asking).toBe('limited');
    expect(latest().retryIn).toBe(3);

    await tick(1000);
    expect(latest().retryIn).toBe(2);
    await tick(1000);
    expect(latest().retryIn).toBe(1);
    await tick(1000);

    expect(latest().retryIn).toBe(0);
    expect(latest().asking).toBe('idle');
    expect(latest().error).toBeNull();
  });
});

describe('QuestionsLive votes', () => {
  it('a toggle shows at once in mine and the votes, and sends the picks with the revision', async () => {
    let answer!: (outcome: VoteOutcome) => void;
    api.putVotes.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    await render(view.room);

    await act(async () => latest().toggleVote(2));

    expect(votes()).toEqual([{ round: ROUND, picks: [1, 2, 4], revision: 3 }]);
    expect(latest().mine).toEqual({ picks: [1, 2, 4], revision: 3 });
    expect(votesOf(2)).toBe(10);

    await act(async () => answer({ type: 'accepted', mine: { picks: [1, 2, 4], revision: 4 } }));

    expect(latest().mine).toEqual({ picks: [1, 2, 4], revision: 4 });
    expect(votesOf(2)).toBe(10);
  });

  it('a toggle on a question that is not open sends nothing', async () => {
    await render(view.room);

    await act(async () => latest().toggleVote(3));

    expect(api.putVotes).not.toHaveBeenCalled();
    expect(votesOf(3)).toBe(6);
  });

  it('a second toggle while one is in flight waits and sends the latest', async () => {
    let answer!: (outcome: VoteOutcome) => void;
    api.putVotes.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    await render(view.room);

    await act(async () => latest().toggleVote(2));
    await act(async () => latest().toggleVote(6));
    await act(async () => latest().toggleVote(1));
    expect(votes()).toHaveLength(1);
    expect(votesOf(6)).toBe(4);
    expect(votesOf(1)).toBe(13);

    await act(async () => answer({ type: 'accepted', mine: { picks: [1, 2, 4], revision: 4 } }));

    expect(votes()[1]).toEqual({ round: ROUND, picks: [2, 4, 6], revision: 4 });
    expect(latest().mine).toEqual({ picks: [2, 4, 6], revision: 5 });
    expect(votesOf(1)).toBe(13);
    expect(votesOf(2)).toBe(10);
    expect(votesOf(6)).toBe(4);
  });

  it('a stale vote takes current and applies the toggle again once', async () => {
    const current = guestQuestions('view', { mine: { picks: [1, 4], revision: 5 } });
    api.putVotes.mockResolvedValueOnce({ type: 'stale', current });
    await render(view.room);

    await act(async () => latest().toggleVote(2));
    await settle();

    expect(votes()).toEqual([
      { round: ROUND, picks: [1, 2, 4], revision: 3 },
      { round: ROUND, picks: [1, 2, 4], revision: 5 },
    ]);
    expect(latest().mine).toEqual({ picks: [1, 2, 4], revision: 6 });
    expect(latest().error).toBeNull();
  });

  it('a second stale in a row drops the toggle and says so', async () => {
    const current = guestQuestions('view', { mine: { picks: [1, 4], revision: 5 } });
    api.putVotes.mockResolvedValue({ type: 'stale', current });
    await render(view.room);

    await act(async () => latest().toggleVote(2));
    await settle();

    expect(votes()).toHaveLength(2);
    expect(latest().mine).toEqual({ picks: [1, 4], revision: 5 });
    expect(latest().error).toBe('stale');
  });

  it('a toggle made while reconnecting waits and is sent after the reconnect read', async () => {
    await render(view.room);
    await render(view.room, 'reconnecting');

    await act(async () => latest().toggleVote(2));
    expect(api.putVotes).not.toHaveBeenCalled();

    await render(view.room, 'live');

    expect(votes()).toEqual([{ round: ROUND, picks: [1, 2, 4], revision: 3 }]);
  });

  it('a refusal drops the toggle and sets error', async () => {
    api.putVotes.mockRejectedValueOnce(new ApiError('picks: unknown question 2', 400));
    await render(view.room);

    await act(async () => latest().toggleVote(2));

    expect(latest().mine).toEqual(view.mine);
    expect(latest().error).toBe('picks: unknown question 2');
  });
});

describe('QuestionsLive after review round 1', () => {
  const asked7: AskOutcome = {
    type: 'asked',
    asked: {
      question: {
        number: 7,
        text: 'Why?',
        name: null,
        age: 'now',
        state: 'open',
        votes: 1,
        onScreen: false,
        yours: true,
      },
      mine: { picks: [1, 4, 7], revision: 4 },
    },
  };
  const nextRoom = { ...view.room, round: OTHER_ROUND, listRevision: 1 };

  it('an upvote taken back while its PUT is out is sent again once the PUT answers', async () => {
    let answer!: (outcome: VoteOutcome) => void;
    api.putVotes.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    await render(view.room);

    await act(async () => latest().toggleVote(2));
    await act(async () => latest().toggleVote(2));
    expect(latest().mine.picks).toEqual([1, 4]);
    await act(async () => answer({ type: 'accepted', mine: { picks: [1, 2, 4], revision: 4 } }));

    expect(votes()).toEqual([
      { round: ROUND, picks: [1, 2, 4], revision: 3 },
      { round: ROUND, picks: [1, 4], revision: 4 },
    ]);
    expect(latest().mine).toEqual({ picks: [1, 4], revision: 5 });
    expect(votesOf(2)).toBe(9);
  });

  it('an upvote taken back while its PUT is out survives a read that already holds that PUT', async () => {
    let answer!: (outcome: VoteOutcome) => void;
    api.putVotes.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    await render(view.room);
    await act(async () => latest().toggleVote(2));
    await act(async () => latest().toggleVote(2));
    const applied = guestQuestions('view', { mine: { picks: [1, 2, 4], revision: 4 } });
    api.getQuestions.mockResolvedValueOnce({
      ...applied,
      room: { ...applied.room, listRevision: view.room.listRevision + 1 },
      questions: applied.questions.map((q) => (q.number === 2 ? { ...q, votes: 10 } : q)),
    });

    await render({ ...view.room, listRevision: view.room.listRevision + 1 });
    expect(latest().mine.picks).toEqual([1, 4]);
    expect(votesOf(2)).toBe(9);
    await act(async () => answer({ type: 'accepted', mine: { picks: [1, 2, 4], revision: 4 } }));

    expect(votes()[1]).toEqual({ round: ROUND, picks: [1, 4], revision: 4 });
  });

  it('a tally that already counts the vote out is not counted twice, and the list is read once it answers', async () => {
    let answer!: (outcome: VoteOutcome) => void;
    api.putVotes.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    await render(view.room);
    await act(async () => latest().toggleVote(2));
    expect(votesOf(2)).toBe(10);

    await hub.push({
      type: 'questionTally',
      round: ROUND,
      listRevision: view.room.listRevision,
      votes: [
        { number: 2, votes: 10 },
        { number: 6, votes: 5 },
      ],
    });
    expect(votesOf(2)).toBe(10);
    expect(votesOf(6)).toBe(5);
    api.getQuestions.mockResolvedValueOnce(
      guestQuestions('view', {
        mine: { picks: [1, 2, 4], revision: 4 },
        questions: view.questions.map((q) => (q.number === 2 ? { ...q, votes: 10 } : q)),
      }),
    );
    await act(async () => answer({ type: 'accepted', mine: { picks: [1, 2, 4], revision: 4 } }));

    expect(votesOf(2)).toBe(10);
    expect(api.getQuestions).toHaveBeenCalledTimes(2);
  });

  it('a question queued in one round is not sent into the next, and says so', async () => {
    api.postQuestion.mockResolvedValue(asked7);
    await render(view.room);
    await render(view.room, 'reconnecting');
    await act(() => latest().ask('Why?', null));
    api.getQuestions.mockResolvedValue({ ...guestQuestions('view'), room: nextRoom });

    await render(nextRoom, 'live');

    expect(api.postQuestion).not.toHaveBeenCalled();
    expect(latest().asking).toBe('failed');
    expect(latest().error).toBe('new_round');
  });

  it('a 201 for the old round that lands after the new round is read is not added to it', async () => {
    let answer!: (outcome: AskOutcome) => void;
    api.postQuestion.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    await render(view.room);
    const asking = act(() => latest().ask('Why?', null));
    api.getQuestions.mockResolvedValue({
      ...guestQuestions('view'),
      room: nextRoom,
      mine: { picks: [], revision: 0 },
    });
    await render(nextRoom, 'live');

    await act(async () => answer(asked7));
    await asking;

    expect(latest().mine).toEqual({ picks: [], revision: 0 });
    expect(latest().questions.map((q) => q.number)).not.toContain(7);
    expect(latest().asking).toBe('idle');
  });

  it('a stale whose current already holds the toggled pick sends nothing more', async () => {
    let answer!: (outcome: VoteOutcome) => void;
    api.putVotes.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    await render(view.room);
    await act(async () => latest().toggleVote(6));

    const current = guestQuestions('view', { mine: { picks: [1, 4, 6], revision: 5 } });
    await act(async () => answer({ type: 'stale', current }));

    expect(votes()).toHaveLength(1);
    expect(latest().mine).toEqual({ picks: [1, 4, 6], revision: 5 });
  });

  it('a queued question is sent once, even when a later read lands', async () => {
    api.postQuestion.mockResolvedValue(asked7);
    await render(view.room, 'reconnecting');
    await act(() => latest().ask('Why?', null));
    await render(view.room, 'live');

    await render({ ...view.room, listRevision: view.room.listRevision + 1 }, 'live');

    // The first read, the reconnect's and the later one.
    expect(api.getQuestions).toHaveBeenCalledTimes(3);
    expect(api.postQuestion).toHaveBeenCalledTimes(1);
  });

  it('a read with an older revision of mine does not move mine back', async () => {
    await render(view.room);
    await act(async () => latest().toggleVote(2));
    expect(latest().mine).toEqual({ picks: [1, 2, 4], revision: 4 });

    await render({ ...view.room, listRevision: view.room.listRevision + 1 });

    expect(api.getQuestions).toHaveBeenCalledTimes(2);
    expect(latest().mine).toEqual({ picks: [1, 2, 4], revision: 4 });
    expect(votesOf(2)).toBe(10);
  });

  it('an older read that lands after a newer one is ignored', async () => {
    let first!: (value: GuestQuestionsView) => void;
    let second!: (value: GuestQuestionsView) => void;
    api.getQuestions
      .mockReturnValueOnce(new Promise((resolve) => (first = resolve)))
      .mockReturnValueOnce(new Promise((resolve) => (second = resolve)));
    await render(view.room);
    const newer = { ...view.room, listRevision: view.room.listRevision + 1 };
    await render(newer);

    await act(async () =>
      second({ ...guestQuestions('view'), room: newer, questions: view.questions.slice(0, 2) }),
    );
    await act(async () => first(guestQuestions('view')));

    expect(latest().questions.map((q) => q.number)).toEqual([1, 2]);
  });

  it('a re-read that fails records no revision: the next tally waits, and the backoff reads it again', async () => {
    vi.useFakeTimers();
    const random = vi.spyOn(Math, 'random').mockReturnValue(0);
    await render(view.room);
    api.getQuestions.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    const higher = {
      type: 'questionTally',
      round: ROUND,
      listRevision: view.room.listRevision + 1,
      votes: [],
    } as const;
    await hub.push({ ...higher, votes: [] });
    expect(api.getQuestions).toHaveBeenCalledTimes(2);
    expect(latest().state).toBe('ready');

    await hub.push({ ...higher, votes: [] });
    expect(api.getQuestions).toHaveBeenCalledTimes(2);
    await tick(REREAD_RETRY_MS);

    expect(api.getQuestions).toHaveBeenCalledTimes(3);
    random.mockRestore();
  });

  it('a limited ask is idle again when the effects run again, since its countdown stopped', async () => {
    api.postQuestion.mockResolvedValueOnce({ type: 'limited', retryAfter: 30 });
    await render(view.room);
    await act(() => latest().ask('Why?', null));
    expect(latest().asking).toBe('limited');

    await render(view.room, 'live', false, 'hidden');
    await render(view.room, 'live', false, 'visible');

    expect(latest().asking).toBe('idle');
    expect(latest().retryIn).toBe(0);
  });
});

describe('QuestionsLive after review round 2', () => {
  const higherTally = (bump: number): QuestionTally => ({
    type: 'questionTally',
    round: ROUND,
    listRevision: view.room.listRevision + bump,
    votes: [],
  });
  const failure = new ApiError('Request failed (500)', 500);
  let random: MockInstance<() => number>;

  beforeEach(() => {
    vi.useFakeTimers();
    random = vi.spyOn(Math, 'random').mockReturnValue(0);
  });

  afterEach(() => random.mockRestore());

  it('a failing GET under a stream of higher tallies and room pushes reads at most 4 times in 10 seconds', async () => {
    await render(view.room);
    api.getQuestions.mockClear();
    api.getQuestions.mockRejectedValue(failure);

    for (let step = 1; step <= 20; step += 1) {
      await tick(500);
      await hub.push(higherTally(1 + Math.floor(step / 4)));
      if (step % 4 === 0)
        await render({ ...view.room, listRevision: view.room.listRevision + 1 + step / 4 });
    }

    // The first failure at 0.5 s, then one read per REREAD_RETRY_MS: 3.5 s, 6.5 s and 9.5 s.
    expect(api.getQuestions).toHaveBeenCalledTimes(4);
    expect(latest().state).toBe('ready');
    expect(latest().error).toBe('Request failed (500)');
  });

  it('the backoff waits REREAD_RETRY_MS plus up to JITTER_MS at random', async () => {
    random.mockReturnValue(0.5);
    await render(view.room);
    api.getQuestions.mockRejectedValueOnce(failure);
    await hub.push(higherTally(1));
    expect(api.getQuestions).toHaveBeenCalledTimes(2);

    await tick(REREAD_RETRY_MS + JITTER_MS / 2 - 1);
    expect(api.getQuestions).toHaveBeenCalledTimes(2);
    await tick(1);
    expect(api.getQuestions).toHaveBeenCalledTimes(3);
  });

  it('a failed minute re-read of a list that is not behind reads nothing more until the next minute', async () => {
    await render(view.room, 'live', true);
    api.getQuestions.mockRejectedValueOnce(failure);
    await tick(60_000);
    expect(api.getQuestions).toHaveBeenCalledTimes(2);

    await tick(59_999);
    expect(api.getQuestions).toHaveBeenCalledTimes(2);
    await tick(1);
    expect(api.getQuestions).toHaveBeenCalledTimes(3);
  });

  it('cleanup clears the backoff timer', async () => {
    await render(view.room);
    api.getQuestions.mockRejectedValueOnce(failure);
    await hub.push(higherTally(1));
    expect(vi.getTimerCount()).toBe(1);

    await render(view.room, 'live', false, 'hidden');

    expect(vi.getTimerCount()).toBe(0);
  });

  it('after a 429 too_many_attempts the backoff is THROTTLED_RETRY_MS plus up to SLOW_JITTER_MS', async () => {
    random.mockReturnValue(0.5);
    await render(view.room);
    api.getQuestions.mockRejectedValueOnce(new ApiError('too_many_attempts', 429));
    await hub.push(higherTally(1));
    expect(api.getQuestions).toHaveBeenCalledTimes(2);

    await tick(THROTTLED_RETRY_MS + SLOW_JITTER_MS / 2 - 1);
    await hub.push(higherTally(1));
    expect(api.getQuestions).toHaveBeenCalledTimes(2);
    await tick(1);

    expect(api.getQuestions).toHaveBeenCalledTimes(3);
  });

  it.each([
    ['no_session', 404],
    ['no_guest', 400],
  ])(
    'after a %s (%i) nothing reads again by itself; retry() reads at once',
    async (code, status) => {
      await render(view.room);
      api.getQuestions.mockRejectedValue(new ApiError(code, status));
      await hub.push(higherTally(1));
      expect(api.getQuestions).toHaveBeenCalledTimes(2);

      for (let step = 1; step <= 60; step += 1) {
        await tick(500);
        await hub.push(higherTally(1 + Math.floor(step / 10)));
      }
      await render({ ...view.room, listRevision: view.room.listRevision + 9 });
      expect(api.getQuestions).toHaveBeenCalledTimes(2);
      expect(latest().error).toBe(code);

      api.getQuestions.mockResolvedValue(guestQuestions('view'));
      await act(async () => latest().retry());

      expect(api.getQuestions).toHaveBeenCalledTimes(3);
      expect(latest().error).toBeNull();
    },
  );

  it('after a failed read a reconnect reads at once, without waiting for the backoff', async () => {
    await render(view.room);
    api.getQuestions.mockRejectedValueOnce(failure);
    await hub.push(higherTally(1));
    expect(api.getQuestions).toHaveBeenCalledTimes(2);

    await render(view.room, 'reconnecting');
    await render(view.room, 'live');

    expect(api.getQuestions).toHaveBeenCalledTimes(3);
    await tick(REREAD_RETRY_MS + JITTER_MS);
    expect(api.getQuestions).toHaveBeenCalledTimes(3);
  });

  it('after a failed read retry() reads at once', async () => {
    await render(view.room);
    api.getQuestions.mockRejectedValueOnce(failure);
    await hub.push(higherTally(1));

    await act(async () => latest().retry());

    expect(api.getQuestions).toHaveBeenCalledTimes(3);
    expect(latest().error).toBeNull();
  });

  it('a question queued before the first read is sent once the first read lands', async () => {
    api.postQuestion.mockResolvedValue({
      type: 'asked',
      asked: {
        question: {
          number: 7,
          text: 'Why?',
          name: null,
          age: 'now',
          state: 'open',
          votes: 1,
          onScreen: false,
          yours: true,
        },
        mine: { picks: [1, 4, 7], revision: 4 },
      },
    });
    await render(null, 'connecting');
    await act(() => latest().ask('Why?', null));
    expect(latest().asking).toBe('queued');

    await render(view.room, 'live');
    await settle();

    expect(api.postQuestion).toHaveBeenCalledTimes(1);
    expect(latest().asking).toBe('idle');
    expect(latest().error).toBeNull();
  });

  it('a stale 409 whose current is a new round drops a queued question, as a new round read does', async () => {
    const nextRoom = { ...view.room, round: OTHER_ROUND, listRevision: 1 };
    const nextView = {
      ...guestQuestions('view'),
      room: nextRoom,
      mine: { picks: [], revision: 0 },
    };
    let answer!: (outcome: VoteOutcome) => void;
    api.putVotes.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    await render(view.room);
    await act(async () => latest().toggleVote(2));
    await render(view.room, 'reconnecting');
    await act(() => latest().ask('Why?', null));
    expect(latest().asking).toBe('queued');

    await act(async () => answer({ type: 'stale', current: nextView }));
    expect(latest().asking).toBe('failed');
    expect(latest().error).toBe('new_round');
    expect(latest().mine).toEqual({ picks: [], revision: 0 });

    api.getQuestions.mockResolvedValue(nextView);
    await render(nextRoom, 'live');
    await settle();

    expect(api.postQuestion).not.toHaveBeenCalled();
    expect(votes()).toHaveLength(1);
  });
});
