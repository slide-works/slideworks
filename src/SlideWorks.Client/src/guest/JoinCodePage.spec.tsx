import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { guestView } from '../test-support/fixtures/guestStates';
import { LocationProbe } from '../test-support/LocationProbe';
import { stubApi } from '../test-support/stubApi';
import { GUEST_TEST_IDS, JOIN_CODE_TEST_IDS, joinCodeBoxTestId } from './testIds';

const miss = { status: 404, json: { error: 'no_session' } };

test('the first box has focus and the boxes use the numeric keypad', async ({ mount, page }) => {
  await stubApi(page, {});
  await mount(<AppRoutes />, { hooksConfig: { route: '/join' } });
  await expect(page.getByTestId(joinCodeBoxTestId(1))).toBeFocused();
  for (let i = 1; i <= 6; i++)
    await expect(page.getByTestId(joinCodeBoxTestId(i))).toHaveAttribute('inputmode', 'numeric');
  await expect(page.getByTestId(joinCodeBoxTestId(1))).toHaveAttribute(
    'autocomplete',
    'one-time-code',
  );
});

test('the sixth digit goes to /j/{code}', async ({ mount, page }) => {
  await stubApi(page, { '/api/join/482913': { json: guestView('lobby') } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/join' } },
  );
  await page.keyboard.type('482913');
  await expect(page.getByTestId('location')).toHaveText('/j/482913');
  await expect(page.getByTestId(GUEST_TEST_IDS.lobby)).toBeVisible();
});

test('pasting "482 913" fills the boxes and joins', async ({ mount, page }) => {
  await stubApi(page, { '/api/join/482913': { json: guestView('lobby') } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/join' } },
  );
  await page.getByTestId(joinCodeBoxTestId(1)).evaluate((box) => {
    const data = new DataTransfer();
    data.setData('text', '482 913');
    box.dispatchEvent(
      new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }),
    );
  });
  await expect(page.getByTestId('location')).toHaveText('/j/482913');
});

test('a miss keeps the digits, shows the advice and focuses it', async ({ mount, page }) => {
  await stubApi(page, { '/api/join/482931': miss });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482931' } });
  const error = page.getByTestId(JOIN_CODE_TEST_IDS.error);
  await expect(error).toBeFocused();
  await expect(error).toContainText('Check the code');
  const values = await Promise.all(
    [1, 2, 3, 4, 5, 6].map((i) => page.getByTestId(joinCodeBoxTestId(i)).inputValue()),
  );
  expect(values.join('')).toBe('482931');
});

test('too many tries says to wait a minute', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/join/482931': { status: 429, json: { error: 'too_many_attempts' } },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482931' } });
  await expect(page.getByTestId(JOIN_CODE_TEST_IDS.error)).toHaveText(
    'Too many tries. Wait a minute and try again.',
  );
});

test('retyping the same code after a miss reads it again', async ({ mount, page }) => {
  let reads = 0;
  await page.route('**/api/join/482931', (route) => {
    reads++;
    return route.fulfill({ status: 404, json: { error: 'no_session' } });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482931' } });
  await expect(page.getByTestId(JOIN_CODE_TEST_IDS.error)).toBeVisible();
  await page.getByTestId(joinCodeBoxTestId(6)).press('Backspace');
  await page.keyboard.type('1');
  await expect.poll(() => reads).toBe(2);
});

test('nothing scrolls sideways at 360 px', async ({ mount, page }) => {
  await page.setViewportSize({ width: 360, height: 640 });
  await stubApi(page, {});
  await mount(<AppRoutes />, { hooksConfig: { route: '/join' } });
  await expect(page.getByTestId(joinCodeBoxTestId(6))).toBeVisible();
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
  const box = await page.getByTestId(joinCodeBoxTestId(1)).boundingBox();
  expect(box!.height).toBeGreaterThanOrEqual(48);
  expect(box!.width).toBeGreaterThanOrEqual(40);
});
