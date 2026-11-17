import { test, expect } from '@playwright/experimental-ct-react';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { OFFLINE_JITTER_MS, OFFLINE_RETRY_MS } from './GuestOffline';
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

test('the shell paints the night surface, not a transparent background', async ({
  mount,
  page,
}) => {
  const view = guestView('lobby');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  const shell = page.getByTestId(GUEST_TEST_IDS.lobby).locator('..');
  expect(await shell.evaluate((el) => getComputedStyle(el).backgroundImage)).toContain('gradient');
});

test('the offline screen fills the screen on the night surface', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 780 });
  const view = guestView('live');
  await mount(<GuestScreens view={view} state={view.state} status="offline" onRetry={noop} />);
  const offline = page.getByTestId(GUEST_TEST_IDS.offline);
  expect(
    await offline.evaluate((el) => getComputedStyle(el.parentElement!).backgroundImage),
  ).toContain('gradient');
  const box = await offline.boundingBox();
  expect(box!.height).toBeGreaterThan(600);
});

test('the offline screen retries by itself after the base wait plus a random extra', async ({
  mount,
  page,
}) => {
  await page.clock.install();
  const view = guestView('live');
  let retries = 0;
  await mount(
    <GuestScreens view={view} state={view.state} status="offline" onRetry={() => retries++} />,
  );
  await page.clock.runFor(OFFLINE_RETRY_MS - 100);
  expect(retries).toBe(0);
  await page.clock.runFor(OFFLINE_JITTER_MS + 200);
  expect(retries).toBe(1);
  // The next wait is longer (it doubles), so the base alone is not enough.
  await page.clock.runFor(OFFLINE_RETRY_MS);
  expect(retries).toBe(1);
});

test('the lobby with a long title and presenter name does not scroll sideways at 360 px', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 740 });
  const view = {
    ...guestView('lobby'),
    deckTitle:
      'A very long deck title that keeps going well past the width of a phone screen'.repeat(2),
    presenterName: 'Maximilian Alexander Featherstonehaugh-Cholmondeley the Third',
  };
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.lobby)).toBeVisible();
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('"You\'re in" shows for 2 seconds, then the slide mirror', async ({ mount, page }) => {
  await page.clock.install();
  const view = guestView('live');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.joined)).toBeVisible();
  await page.clock.runFor(1900);
  await expect(page.getByTestId(GUEST_TEST_IDS.joined)).toBeVisible();
  await page.clock.runFor(200);
  await expect(page.getByTestId(GUEST_TEST_IDS.following)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.joined)).toHaveCount(0);
});

test('the joined screen keeps the main landmark and a status inside it', async ({
  mount,
  page,
}) => {
  const view = guestView('live');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  await expect(page.getByRole('main')).toHaveCount(1);
  await expect(page.getByRole('status')).toContainText("You're in");
});

test('the offline retry button name contains its visible text, and "Guest" shows once in the footer', async ({
  mount,
  page,
}) => {
  const view = guestView('live');
  await mount(<GuestScreens view={view} state={view.state} status="offline" onRetry={noop} />);
  await expect(page.getByRole('button', { name: /Try now/ })).toBeVisible();
  await expect(page.locator('footer')).toContainText('Guest · no account');
  await expect(page.locator('footer')).not.toContainText('Guest · Privacy');
  expect((await page.locator('footer').innerText()).match(/Guest/g)).toHaveLength(1);
});

test('the privacy sheet closes with Escape and focus returns to the footer button', async ({
  mount,
  page,
}) => {
  const view = guestView('lobby');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  const open = page.getByTestId(GUEST_TEST_IDS.privacyOpen);
  await open.click();
  await expect(page.getByTestId(GUEST_TEST_IDS.privacySheet)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(GUEST_TEST_IDS.privacySheet)).toHaveCount(0);
  await expect(open).toBeFocused();
});

test('needs_cookies status → the cookies screen, and its button retries', async ({
  mount,
  page,
}) => {
  const view = guestView('live');
  let retries = 0;
  await mount(
    <GuestScreens
      view={view}
      state={view.state}
      status="needs_cookies"
      onRetry={() => retries++}
    />,
  );
  await expect(page.getByTestId(GUEST_TEST_IDS.needsCookies)).toContainText('needs cookies');
  await page.getByTestId(GUEST_TEST_IDS.needsCookiesRetry).click();
  expect(retries).toBe(1);
});

test('screenshot: lobby at phone width', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 780 });
  const view = guestView('lobby');
  await mount(<GuestScreens view={view} state={view.state} status="live" onRetry={noop} />);
  await expect(page).toHaveScreenshot('guest-lobby.png');
});
