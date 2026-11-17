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
import { POLL_CARD_TEST_IDS, pollOptionTestId } from '../testIds';

// The guest page with a poll on screen: ActivitySlot mounts PollAnswerLive over the real audience connection (the stub
// hub) and the poll routes (the stub API), whose watch rule follows the view.
async function guestOnPoll(page: Page, activity: PollActivity, poll: GuestPollView) {
  const view = guestView('live');
  const state = { ...view.state, activity };
  const hub = await stubDeckHub(page, { audience: { state } });
  await stubApi(page, { '/api/join/482913': { json: { ...view, state } } });
  const api = await stubPollApi(page, { view: poll, hub });
  return { hub, api, state };
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

test('a read the server refuses shows a plain sentence on the card, never its code', async ({
  mount,
  page,
}) => {
  const live = pollActivity('open', { pick: 'one', max: null, results: 'live' });
  const { api } = await guestOnPoll(page, live, { ...guestPoll('notAnswered'), activity: live });
  api.queueGet({ status: 429, json: { error: 'too_many_attempts' } });

  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });

  await expect(page.getByTestId(POLL_CARD_TEST_IDS.failed)).toHaveText(
    'Too many tries from this network right now. Wait a moment, then try again.',
  );
});

const pickOne = pollActivity('open', { pick: 'one', max: null, results: 'live' });
const notAnswered = (activity: PollActivity): GuestPollView => ({
  ...guestPoll('notAnswered'),
  activity,
  tally: { type: 'pollTally', round: activity.round!, answered: 0, counts: [0, 0, 0, 0] },
});

test('nothing can be tapped until the first read lands', async ({ mount, page }) => {
  const { api } = await guestOnPoll(page, pickOne, notAnswered(pickOne));
  api.queueGet({ hold: true });

  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });

  await expect(page.getByTestId(pollOptionTestId(1))).toBeDisabled();
  await api.release();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeEnabled();
});

test('a tap sends the pick with the current revision, and another tap replaces it with the next', async ({
  mount,
  page,
}) => {
  const { api } = await guestOnPoll(page, pickOne, notAnswered(pickOne));
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });

  await page.getByTestId(pollOptionTestId(2)).check();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.sent)).toBeVisible();
  await page.getByTestId(pollOptionTestId(0)).check();

  await expect.poll(() => api.view.mine).toEqual({ picks: [0], revision: 2 });
  expect(api.answers).toEqual([
    { round: pickOne.round, picks: [2], revision: 0 },
    { round: pickOne.round, picks: [0], revision: 1 },
  ]);
  await expect(page.getByTestId(pollOptionTestId(0))).toBeChecked();
});

test('a 409 keeps the pick and shows the conflict', async ({ mount, page }) => {
  const { api } = await guestOnPoll(page, pickOne, notAnswered(pickOne));
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(pollOptionTestId(1))).toBeEnabled();
  // The guest answered from another tab meanwhile, so the phone's revision 0 is stale.
  api.setView({ ...api.view, mine: { picks: [3], revision: 1 } });

  await page.getByTestId(pollOptionTestId(1)).check();

  await expect(page.getByTestId(POLL_CARD_TEST_IDS.conflict)).toBeVisible();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeChecked();
  expect(api.answers).toEqual([{ round: pickOne.round, picks: [1], revision: 0 }]);
  expect(api.view.mine).toEqual({ picks: [3], revision: 1 });
});

test('a pick made while reconnecting is queued and sent once the connection is back', async ({
  mount,
  page,
}) => {
  test.setTimeout(45_000);
  const { hub, api } = await guestOnPoll(page, pickOne, notAnswered(pickOne));
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(pollOptionTestId(1))).toBeEnabled();
  // The rejoin is refused with too_many_attempts, which holds the phone in reconnecting until the slow retry.
  hub.refuseAudience('too_many_attempts');
  hub.drop();
  await expect(page.getByTestId(GUEST_TEST_IDS.chip)).toHaveText('Offline');

  await page.getByTestId(pollOptionTestId(1)).check();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.queued)).toBeVisible();
  expect(api.answers).toEqual([]);
  hub.refuseAudience(undefined);

  await expect
    .poll(() => api.answers, { timeout: 20_000 })
    .toEqual([{ round: pickOne.round, picks: [1], revision: 0 }]);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.sent)).toBeVisible();
});

test('a queued pick is dropped with a message when the poll closed while the phone was away', async ({
  mount,
  page,
}) => {
  test.setTimeout(45_000);
  const { hub, api, state } = await guestOnPoll(page, pickOne, notAnswered(pickOne));
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(pollOptionTestId(1))).toBeEnabled();
  hub.refuseAudience('too_many_attempts');
  hub.drop();
  await expect(page.getByTestId(GUEST_TEST_IDS.chip)).toHaveText('Offline');
  await page.getByTestId(pollOptionTestId(1)).check();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.queued)).toBeVisible();

  const closed: PollActivity = { ...pickOne, phase: 'closed', roomSeesCounts: true };
  api.setView({ ...api.view, activity: closed });
  hub.setAudienceState({ ...state, seq: state.seq + 5, activity: closed });
  hub.refuseAudience(undefined);

  await expect(page.getByTestId(POLL_CARD_TEST_IDS.failed)).toHaveText(
    'The poll closed before your answer could be sent.',
    { timeout: 20_000 },
  );
  expect(api.answers).toEqual([]);
});

const pickSeveral = pollActivity('open', { pick: 'upTo', max: 2, results: 'live' });

test('Change my answer keeps the sent picks ticked, and Send comes back once they differ', async ({
  mount,
  page,
}) => {
  const { api } = await guestOnPoll(page, pickSeveral, notAnswered(pickSeveral));
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await page.getByTestId(pollOptionTestId(0)).check();
  await page.getByTestId(pollOptionTestId(2)).check();
  await page.getByTestId(POLL_CARD_TEST_IDS.send).click();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.sent)).toBeVisible();

  await page.getByTestId(POLL_CARD_TEST_IDS.change).click();

  await expect(page.getByTestId(pollOptionTestId(0))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(2))).toBeChecked();
  await page.getByTestId(pollOptionTestId(2)).uncheck();
  await page.getByTestId(pollOptionTestId(3)).check();
  await page.getByTestId(POLL_CARD_TEST_IDS.send).click();
  await expect.poll(() => api.view.mine).toEqual({ picks: [0, 3], revision: 2 });
  expect(api.answers).toEqual([
    { round: pickSeveral.round, picks: [0, 2], revision: 0 },
    { round: pickSeveral.round, picks: [0, 3], revision: 1 },
  ]);
});

test('Clear after a conflict drops the kept pick and shows what the server holds', async ({
  mount,
  page,
}) => {
  const { api } = await guestOnPoll(page, pickSeveral, notAnswered(pickSeveral));
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(pollOptionTestId(1))).toBeEnabled();
  // The guest answered from another tab meanwhile, so the phone's revision 0 is stale.
  api.setView({ ...api.view, mine: { picks: [3], revision: 1 } });
  await page.getByTestId(pollOptionTestId(1)).check();
  await page.getByTestId(POLL_CARD_TEST_IDS.send).click();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.conflict)).toBeVisible();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeChecked();

  await page.getByTestId(POLL_CARD_TEST_IDS.conflictClear).click();

  await expect(page.getByTestId(POLL_CARD_TEST_IDS.conflict)).toHaveCount(0);
  await expect(page.getByTestId(pollOptionTestId(1))).not.toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(3))).toBeChecked();
  expect(api.answers).toHaveLength(1);
});
