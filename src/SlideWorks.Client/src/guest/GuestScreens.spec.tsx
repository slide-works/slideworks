import { test, expect } from '@playwright/experimental-ct-react';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { GuestScreens } from './GuestScreens';
import { GUEST_TEST_IDS } from './testIds';

const noop = () => {};

test('lobby phase → the lobby, with no deck logo', async ({ mount, page }) => {
  const view = guestView('lobby');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.lobby)).toBeVisible();
  await expect(page.locator('img')).toHaveCount(0);
});

test('ended phase → thanks, with the slides link as a plain anchor when deckUrl is set', async ({
  mount,
  page,
}) => {
  const view = guestView('ended');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.ended)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.endedSlides)).toHaveAttribute('href', view.deckUrl!);
});

test('ended phase with no deckUrl → no slides link', async ({ mount, page }) => {
  const view = { ...guestView('ended'), deckUrl: null };
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.ended)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.endedSlides)).toHaveCount(0);
});

test('live phase → "you\'re in" first', async ({ mount, page }) => {
  const view = guestView('live');
  await mount(<GuestScreens view={view} state={audienceState()} status="live" onRetry={noop} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.joined)).toBeVisible();
});

test('offline status → the offline screen, and Try now calls onRetry', async ({ mount, page }) => {
  const view = guestView('live');
  let retries = 0;
  await mount(
    <GuestScreens view={view} state={view.state} status="offline" onRetry={() => retries++} />,
  );
  await expect(page.getByTestId(GUEST_TEST_IDS.offline)).toBeVisible();
  await page.getByTestId(GUEST_TEST_IDS.offlineRetry).click();
  expect(retries).toBe(1);
});

test('reconnecting status → the banner over the last screen', async ({ mount, page }) => {
  const view = guestView('lobby');
  await mount(<GuestScreens view={view} state={view.state} status="reconnecting" onRetry={noop} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.reconnecting)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.lobby)).toBeVisible();
});

test('privacy sheet → four lines, and focus returns to the footer button on close', async ({
  mount,
  page,
}) => {
  const view = guestView('lobby');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  const open = page.getByTestId(GUEST_TEST_IDS.privacyOpen);
  await open.click();
  const sheet = page.getByTestId(GUEST_TEST_IDS.privacySheet);
  await expect(sheet.locator('li')).toHaveText([
    'No account and no name. We keep a random ID in this browser so you count once.',
    'The presenter sees totals, not who answered what.',
    'No ads, no trackers, no third-party scripts.',
    'Your answers are kept with this session and deleted with it.',
  ]);
  await expect(page.getByTestId(GUEST_TEST_IDS.privacyClose)).toBeFocused();
  await page.getByTestId(GUEST_TEST_IDS.privacyClose).click();
  await expect(sheet).toHaveCount(0);
  await expect(open).toBeFocused();
});

test('phone at 360px → nothing scrolls sideways', async ({ mount, page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
  const view = guestView('ended');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(overflow).toBe(false);
});

test('screenshot: lobby at phone width', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 780 });
  const view = guestView('lobby');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  await expect(page).toHaveScreenshot('guest-lobby.png');
});
