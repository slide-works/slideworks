import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import { GUEST_TEST_IDS } from '../../guest/testIds';
import { guestView } from '../../test-support/fixtures/guestStates';
import { guestPoll, pollActivity } from '../../test-support/fixtures/pollStates';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { stubPollApi } from '../../test-support/stubPollApi';
import type { GuestPollView, PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';

// The guest page with a poll on screen: ActivitySlot mounts PollAnswerLive over the real audience connection (the stub
// hub) and the poll routes (the stub API), whose watch rule follows the view.
async function guestOnPoll(page: Page, activity: PollActivity, poll: GuestPollView) {
  const view = guestView('live');
  const state = { ...view.state, activity };
  const hub = await stubDeckHub(page, { audience: { state } });
  await stubApi(page, { '/api/join/482913': { json: { ...view, state } } });
  const api = await stubPollApi(page, { view: poll, hub });
  return { hub, api };
}

const afterAnswer = pollActivity('open', { pick: 'one', max: null, results: 'afterAnswer' });
const answeredView = (activity: PollActivity): GuestPollView => ({
  ...guestPoll('notAnswered'),
  activity,
  mine: { picks: [1], revision: 1 },
  tally: { type: 'pollTally', round: activity.round!, answered: 3, counts: [0, 2, 1, 0] },
});

test('reads the poll once on load and watches its results when the guest has answered an afterAnswer poll', async ({
  mount,
  page,
}) => {
  const { hub, api } = await guestOnPoll(page, afterAnswer, answeredView(afterAnswer));

  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });

  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toBeVisible();
  await expect.poll(() => hub.watchingPoll).toBe(true);
  expect(hub.pollWatches).toEqual(['482913']);
  expect(api.gets).toBe(1);
});

test('after a reconnect it watches the results again and reads the poll again', async ({
  mount,
  page,
}) => {
  const { hub, api } = await guestOnPoll(page, afterAnswer, answeredView(afterAnswer));
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect.poll(() => hub.watchingPoll).toBe(true);

  hub.drop();

  await expect.poll(() => hub.audienceJoins.length).toBe(2);
  await expect.poll(() => hub.pollWatches).toEqual(['482913', '482913']);
  await expect.poll(() => hub.watchingPoll).toBe(true);
  await expect.poll(() => api.gets).toBe(2);
  await expect(page.getByTestId(GUEST_TEST_IDS.chip)).toHaveText('Live');
});

test('does not watch before the guest has answered', async ({ mount, page }) => {
  const { hub, api } = await guestOnPoll(page, afterAnswer, {
    ...answeredView(afterAnswer),
    mine: null,
    tally: { type: 'pollTally', round: afterAnswer.round!, answered: 3, counts: null },
  });

  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });

  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toBeVisible();
  await expect.poll(() => api.gets).toBe(1);
  expect(hub.pollWatches).toEqual([]);
});

test('reads again when the phase changes, and not when the same poll comes again', async ({
  mount,
  page,
}) => {
  const live = pollActivity('open', { pick: 'one', max: null, results: 'live' });
  const { hub, api } = await guestOnPoll(page, live, {
    ...guestPoll('notAnswered'),
    activity: live,
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect.poll(() => api.gets).toBe(1);

  hub.pushActivity({ ...live });
  const closed: PollActivity = { ...live, phase: 'closed' };
  api.setView({ ...api.view, activity: closed });
  hub.pushActivity(closed);

  await expect.poll(() => api.gets).toBe(2);
  expect(api.gets).toBe(2);
});

test('a read the server refuses shows its error on the card', async ({ mount, page }) => {
  const live = pollActivity('open', { pick: 'one', max: null, results: 'live' });
  const { api } = await guestOnPoll(page, live, { ...guestPoll('notAnswered'), activity: live });
  api.queueGet({ status: 429, json: { error: 'too_many_attempts' } });

  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });

  await expect(page.getByTestId(POLL_CARD_TEST_IDS.failed)).toHaveText('too_many_attempts');
});
