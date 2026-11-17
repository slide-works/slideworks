import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { GuestFollowing } from './GuestFollowing';
import { GUEST_TEST_IDS } from './testIds';

const view = guestView('live');

test('mirrors state.current at the phone width with the waiting card and no sideways scroll', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 740 });
  await mount(<GuestFollowing view={view} state={view.state} />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);

  await expect(slide).toHaveAttribute('data-slide', '3');
  await expect(slide.getByTestId('slide-3')).toHaveCount(1);
  await expect(page.getByTestId(GUEST_TEST_IDS.progress)).toHaveText('3 / 12');
  await expect(page.getByTestId(GUEST_TEST_IDS.activityWaiting)).toHaveText(
    'Nothing to answer yet',
  );
  const box = await slide.boundingBox();
  expect(box!.width).toBeLessThanOrEqual(360);
  expect(Math.round((box!.width / box!.height) * 100)).toBe(178);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('a black state shows a black screen with no slide in it', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const state = { ...view.state, current: { ...view.state.current!, black: true } };
  await mount(<GuestFollowing view={view} state={state} />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);

  await expect(slide).toHaveAttribute('data-black', 'true');
  await expect(slide).toHaveCSS('background-color', 'rgb(0, 0, 0)');
  await expect(slide.getByTestId('slide-3')).toHaveCount(0);
});

test('landscape fills the screen with the slide and hides the card', async ({ mount, page }) => {
  await page.setViewportSize({ width: 844, height: 390 });
  await mount(<GuestFollowing view={view} state={view.state} />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);

  await expect(page.getByTestId(GUEST_TEST_IDS.activity)).toBeHidden();
  const box = await slide.boundingBox();
  expect(Math.round(box!.height)).toBe(390);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('the page follows the presenter: a pushed state moves the slide', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  await stubApi(page, { '/api/join/482913': { json: view } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);
  await expect(slide).toHaveAttribute('data-slide', '3');

  hub.pushAudienceEvent(
    audienceState({
      seq: 43,
      current: {
        ...view.state.current!,
        index: 4,
        layout: 'divider',
        slots: { title: 'Next part' },
      } as never,
    }),
  );

  await expect(slide).toHaveAttribute('data-slide', '4');
  await expect(page.getByTestId(GUEST_TEST_IDS.progress)).toHaveText('4 / 12');
});

test('the page shows the ended screen on audienceEnded', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  await stubApi(page, { '/api/join/482913': { json: view } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.slide)).toBeVisible();

  hub.pushAudienceEvent({ type: 'audienceEnded' });

  await expect(page.getByTestId(GUEST_TEST_IDS.ended)).toBeVisible();
});

test('GuestFollowing screenshot at 390 wide', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mount(<GuestFollowing view={view} state={view.state} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.slide).getByTestId('slide-3')).toHaveCount(1);
  await expect(page).toHaveScreenshot('guest-following-390.png');
});

test('GuestFollowing screenshot in landscape', async ({ mount, page }) => {
  await page.setViewportSize({ width: 844, height: 390 });
  await mount(<GuestFollowing view={view} state={view.state} />);
  await expect(page.getByTestId(GUEST_TEST_IDS.slide).getByTestId('slide-3')).toHaveCount(1);
  await expect(page).toHaveScreenshot('guest-following-landscape.png');
});
