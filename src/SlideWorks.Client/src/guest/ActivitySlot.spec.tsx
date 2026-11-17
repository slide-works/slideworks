import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { POLL_CARD_TEST_IDS } from '../polls/testIds';
import { AudienceHarness } from '../test-support/AudienceHarness';
import { guestView } from '../test-support/fixtures/guestStates';
import { guestPoll, pollActivity } from '../test-support/fixtures/pollStates';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { stubPollApi } from '../test-support/stubPollApi';
import { ActivitySlot } from './ActivitySlot';
import { GUEST_TEST_IDS } from './testIds';

test('with no activity the slot is SW-5’s waiting card', async ({ mount, page }) => {
  await mount(<ActivitySlot code="482913" activity={null} status="live" presenter="Sam" />);

  await expect(page.getByTestId(GUEST_TEST_IDS.activityWaiting)).toHaveText(
    'Nothing to answer yet',
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toHaveCount(0);
});

test('with a poll activity the slot is the poll card', async ({ mount, page }) => {
  const activity = pollActivity('open');
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await mount(
    <AudienceHarness code="482913" view={guestView('live')}>
      <ActivitySlot code="482913" activity={activity} status="live" presenter="Sam" />
    </AudienceHarness>,
  );

  await expect(page.getByTestId(GUEST_TEST_IDS.activity)).toBeVisible();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toBeVisible();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.question)).toHaveText(activity.question);
  await expect(page.getByTestId(GUEST_TEST_IDS.activityWaiting)).toHaveCount(0);
});

test('on the guest page the card follows the activity on the audience state', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 740 });
  const view = guestView('live');
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  await stubApi(page, { '/api/join/482913': { json: view } });
  await stubPollApi(page, { view: guestPoll('notAnswered') });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.activityWaiting)).toBeVisible();

  hub.pushActivity(pollActivity('open'));

  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toBeVisible();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.privacy)).toHaveText(
    `Private: ${view.presenterName} sees totals only`,
  );
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);

  hub.pushActivity(null);

  await expect(page.getByTestId(GUEST_TEST_IDS.activityWaiting)).toBeVisible();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toHaveCount(0);
});
