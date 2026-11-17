import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { ReactElement } from 'react';
import { AppRoutes } from '../AppRoutes';
import { guestView } from '../test-support/fixtures/guestStates';
import { LocationProbe } from '../test-support/LocationProbe';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';
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
  await stubDeckHub(page, { audience: { state: guestView('lobby').state } });
  await stubApi(page, { '/api/join/482913': { json: guestView('lobby') } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/join' } },
  );
  await expect(page.getByTestId(joinCodeBoxTestId(1))).toBeFocused();
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
  await expect(page.getByTestId(joinCodeBoxTestId(1))).toBeFocused();
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

const values = (page: Page) =>
  Promise.all(
    [1, 2, 3, 4, 5, 6].map((i) => page.getByTestId(joinCodeBoxTestId(i)).inputValue()),
  ).then((all) => all.join('|'));

type Mount = (
  component: ReactElement,
  options: { hooksConfig: { route: string } },
) => Promise<unknown>;

async function mountKeypad(mount: Mount, page: Page) {
  await stubDeckHub(page, { audience: { state: guestView('lobby').state } });
  await stubApi(page, { '/api/join/482913': { json: guestView('lobby') } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/join' } });
  await expect(page.getByTestId(joinCodeBoxTestId(1))).toBeFocused();
}

test('the Delete key empties a box', async ({ mount, page }) => {
  await mountKeypad(mount, page);
  await page.keyboard.type('4829');
  await page.getByTestId(joinCodeBoxTestId(2)).focus();
  await page.keyboard.press('Delete');
  await expect.poll(() => values(page)).toBe('4||2|9||');
});

test('a box emptied by an input event with no keydown is cleared', async ({ mount, page }) => {
  await mountKeypad(mount, page);
  await page.keyboard.type('482');
  // What Gboard sends: key "Unidentified", then an input event with the digit gone.
  await page.getByTestId(joinCodeBoxTestId(3)).focus();
  await page.getByTestId(joinCodeBoxTestId(3)).evaluate((box: HTMLInputElement) => {
    const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
    set.call(box, '');
    box.dispatchEvent(new Event('input', { bubbles: true }));
  });
  await expect.poll(() => values(page)).toBe('4|8||||');
});

test('Cut empties a box', async ({ mount, page }) => {
  await mountKeypad(mount, page);
  await page.keyboard.type('482');
  await page.getByTestId(joinCodeBoxTestId(1)).focus();
  await page.keyboard.press('ControlOrMeta+x');
  await expect.poll(() => values(page)).toBe('|8|2|||');
});

test('Backspace in an empty box goes back one box', async ({ mount, page }) => {
  await mountKeypad(mount, page);
  await page.keyboard.type('48');
  await page.keyboard.press('Backspace');
  await page.keyboard.press('Backspace');
  await expect(page.getByTestId(joinCodeBoxTestId(2))).toBeFocused();
  await expect.poll(() => values(page)).toBe('4|||||');
});

test('a one-time code appended to a digit keeps the last six digits', async ({ mount, page }) => {
  await mountKeypad(mount, page);
  await page.getByTestId(joinCodeBoxTestId(1)).evaluate((box: HTMLInputElement) => {
    const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
    set.call(box, '4482913');
    box.dispatchEvent(new Event('input', { bubbles: true }));
  });
  await expect(page).toHaveURL(/./);
  await expect(page.getByTestId(GUEST_TEST_IDS.lobby)).toBeVisible();
});

test('tapping a later box first sends focus to the first empty box', async ({ mount, page }) => {
  await mountKeypad(mount, page);
  await page.keyboard.type('48');
  await page.getByTestId(joinCodeBoxTestId(5)).focus();
  await expect(page.getByTestId(joinCodeBoxTestId(3))).toBeFocused();
});

test('a code starting with 0 says so and focuses the error every time', async ({ mount, page }) => {
  await mountKeypad(mount, page);
  const error = page.getByTestId(JOIN_CODE_TEST_IDS.error);
  await page.keyboard.type('012345');
  await expect(error).toHaveText("Join codes don't start with 0. Check the code on the screen.");
  await expect(error).toBeFocused();
  await page.getByTestId(joinCodeBoxTestId(6)).focus();
  await page.keyboard.type('6');
  await expect(error).toBeFocused();
  await page.getByTestId(joinCodeBoxTestId(6)).focus();
  await page.keyboard.type('7');
  await expect(error).toBeFocused();
});

test('a miss shows the code spaced in bold with red boxes on the night surface', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await stubApi(page, { '/api/join/482931': miss });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482931' } });
  const error = page.getByTestId(JOIN_CODE_TEST_IDS.error);
  await expect(error.locator('b')).toHaveText('482 931');
  await expect(page.getByTestId(joinCodeBoxTestId(1))).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('button', { name: 'Clear and try again' })).toBeVisible();
  const page_ = page.getByTestId(JOIN_CODE_TEST_IDS.page);
  await expect(page_).toHaveCSS('background-image', /radial-gradient/);
  await expect(page_).toHaveCSS('background-color', 'rgb(11, 16, 32)');
});

test('screenshot: the empty keypad', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await stubApi(page, {});
  await mount(<AppRoutes />, { hooksConfig: { route: '/join' } });
  await expect(page.getByTestId(joinCodeBoxTestId(1))).toBeFocused();
  await expect(page).toHaveScreenshot('join-code-empty.png', { maxDiffPixels: 200 });
});

test('screenshot: the wrong code', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await stubApi(page, { '/api/join/482931': miss });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482931' } });
  await expect(page.getByTestId(JOIN_CODE_TEST_IDS.error)).toBeFocused();
  await expect(page).toHaveScreenshot('join-code-error.png', { maxDiffPixels: 200 });
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
