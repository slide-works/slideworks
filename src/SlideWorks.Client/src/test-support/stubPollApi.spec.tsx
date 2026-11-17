import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { AnswerRequest, GuestPollView } from '../polls/types';
import { guestPoll, pollActivity } from './fixtures/pollStates';
import { stubPollApi } from './stubPollApi';

const ROUND = '0b7c6a52-1f0e-4f43-9d3a-5a1c2e8b7d10';
const PATH = '/api/join/482913/poll';

// An open pick-one poll with no answer yet.
const openOne = (): GuestPollView => ({
  activity: pollActivity('open', { pick: 'one', max: null }),
  mine: null,
  tally: null,
  joinedLate: false,
});

// What the page's fetch got back: the status and body, or status 0 when the call never reached the server.
const put = (page: Page, body: Partial<AnswerRequest>) =>
  page.evaluate(
    async ([path, request]) => {
      try {
        const response = await fetch(path, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request),
        });
        return { status: response.status, json: (await response.json()) as unknown };
      } catch {
        return { status: 0, json: null as unknown };
      }
    },
    [`${PATH}/answer`, { round: ROUND, picks: [1], revision: 0, ...body }] as const,
  );

const get = (page: Page) =>
  page.evaluate(async (path) => (await fetch(path)).json() as Promise<GuestPollView>, PATH);

test('an answer to the open round at the current revision is accepted and kept for the next GET', async ({
  page,
}) => {
  const api = await stubPollApi(page, { view: openOne() });

  expect(await put(page, { picks: [2] })).toEqual({
    status: 200,
    json: { picks: [2], revision: 1 },
  });
  expect((await get(page)).mine).toEqual({ picks: [2], revision: 1 });
  expect(await put(page, { picks: [0], revision: 1 })).toEqual({
    status: 200,
    json: { picks: [0], revision: 2 },
  });
  expect(api.answers.map((a) => a.picks)).toEqual([[2], [0]]);
});

test('a stale answer is a 409 with the current view, and writes nothing', async ({ page }) => {
  const api = await stubPollApi(page, { view: openOne() });
  const stale = { status: 409, json: { error: 'stale', current: openOne() } };

  expect(await put(page, { round: 'another-round' })).toEqual(stale);
  expect(await put(page, { revision: 3 })).toEqual(stale);
  api.setView({ ...openOne(), activity: pollActivity('closed') });
  expect((await put(page, {})).status).toBe(409);
  api.setView({ ...openOne(), activity: pollActivity('draft') });
  expect((await put(page, { round: null as never })).status).toBe(409);

  expect(api.view.mine).toBeNull();
});

test('each broken picks rule is a 400 naming it', async ({ page }) => {
  await stubPollApi(page, { view: openOne() });
  const refusal = async (picks: unknown) => (await put(page, { picks: picks as number[] })).json;

  expect(await refusal([])).toEqual({ error: 'picks: required' });
  expect(await refusal([7])).toEqual({ error: 'picks: unknown option 7' });
  expect(await refusal([1, 1])).toEqual({ error: 'picks: duplicate' });
  expect(await refusal([0, 1])).toEqual({ error: 'picks: pick one' });
});

test('pick several refuses more than max', async ({ page }) => {
  const view = { ...openOne(), activity: pollActivity('open') };
  await stubPollApi(page, { view });

  expect((await put(page, { picks: [0, 1, 2] })).json).toEqual({ error: 'picks: at most 2' });
  expect((await put(page, { picks: [2, 0] })).json).toEqual({ picks: [0, 2], revision: 1 });
});

test('scripted and held replies answer with any status, or fail before the server', async ({
  page,
}) => {
  const api = await stubPollApi(page, { view: openOne() });
  api.queueAnswer({ status: 429, json: { error: 'too_many_answers' } });
  api.queueAnswer({ networkDown: true });
  api.queueAnswer({ hold: true, status: 409, json: { error: 'stale', current: openOne() } });

  expect(await put(page, {})).toEqual({ status: 429, json: { error: 'too_many_answers' } });
  expect(await put(page, {})).toEqual({ status: 0, json: null });
  const held = put(page, {});
  await expect.poll(() => api.answers.length).toBe(3);
  await api.release();
  expect((await held).status).toBe(409);
});

test('a held unscripted answer is checked when released, against the view as it is then', async ({
  page,
}) => {
  const api = await stubPollApi(page, { view: openOne() });
  api.queueAnswer({ hold: true });

  const held = put(page, {});
  await expect.poll(() => api.answers.length).toBe(1);
  api.setView({ ...openOne(), activity: pollActivity('closed') });
  await api.release();

  expect((await held).status).toBe(409);
});

test('the hub option allows WatchPoll once the guest has answered an open afterAnswer poll', async ({
  page,
}) => {
  const watchable: boolean[] = [];
  const hub = { setPollWatchable: (on: boolean) => watchable.push(on) };
  await stubPollApi(page, { view: { ...guestPoll('notAnswered') }, hub });

  await put(page, { picks: [1] });

  expect(watchable).toEqual([false, true]);
});
