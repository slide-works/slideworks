import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { sampleDeck } from '../test-support/fixtures/screens';
import { mountPlayer } from '../test-support/mountPlayer';
import { stubApi } from '../test-support/stubApi';
import { RevealPlayer } from './test-support/RevealPlayer';

// The player over a layout built on the real Reveal (RevealTestLayout stands
// in for the bullets stub). Slide 3 is the sample deck's bullets slide: four
// steps, one focusable item per step.
const ITEMS = [1, 2, 3, 4];

async function openAt(page: Page, hash: string) {
  await page.evaluate((h) => window.history.replaceState(null, '', h), hash);
}

async function expectHash(page: Page, hash: string) {
  await expect.poll(() => page.evaluate(() => window.location.hash)).toBe(hash);
}

const item = (page: Page, at: number) =>
  page.getByTestId('player-slide-3').getByTestId(`reveal-item-${at}`);

// Tabs through the whole page (more stops than it has) and lists where focus landed.
async function tabStops(page: Page): Promise<string[]> {
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  const stops: string[] = [];
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press('Tab');
    stops.push(
      await page.evaluate(
        () =>
          document.activeElement?.getAttribute('data-testid') ?? document.activeElement!.tagName,
      ),
    );
  }
  return stops;
}

test.beforeEach(async ({ page }) => {
  await stubApi(page, {});
});

test('the active slide reveals its items one step at a time', async ({ mount, page }) => {
  await openAt(page, '#3.2');
  await mountPlayer(mount, page, <RevealPlayer deck={sampleDeck} />);

  await expect(item(page, 1)).toBeVisible();
  await expect(item(page, 2)).toBeVisible();
  await expect(item(page, 3)).toBeHidden();
  await expect(item(page, 4)).toBeHidden();
  // The shown items on the showing slide can take focus.
  expect(await tabStops(page)).toContain('reveal-item-1');
});

test('a passed stepped slide shows nothing and takes no focus', async ({ mount, page }) => {
  await openAt(page, '#3');
  await mountPlayer(mount, page, <RevealPlayer deck={sampleDeck} />);
  for (const at of ITEMS) {
    await page.keyboard.press('ArrowRight');
    await expect(item(page, at)).toBeVisible();
  }
  await page.keyboard.press('ArrowRight');
  await expectHash(page, '#4');

  const layer = page.getByTestId('player-slide-3');
  await expect(layer).toHaveAttribute('inert', '');
  for (const at of ITEMS) {
    await expect(item(page, at)).toBeHidden();
    await expect(item(page, at)).not.toBeFocused();
  }
  const stops = await tabStops(page);
  expect(stops.filter((stop) => stop.startsWith('reveal-item-'))).toEqual([]);
  // Tab still reaches the controls, so the walk did move focus.
  expect(stops).toContain('player-next');

  // A focus() call can't reach them either: the layer is inert.
  await item(page, 1).evaluate((button) => (button as HTMLButtonElement).focus());
  await expect(item(page, 1)).not.toBeFocused();

  // Going back shows every step again on the now active slide (D2).
  await page.keyboard.press('ArrowLeft');
  await expectHash(page, '#3.4');
  await expect(layer).not.toHaveAttribute('inert');
  for (const at of ITEMS) {
    await expect(item(page, at)).toBeVisible();
  }
});

test('a stepped slide still to come shows nothing and takes no focus', async ({ mount, page }) => {
  await openAt(page, '#2');
  await mountPlayer(mount, page, <RevealPlayer deck={sampleDeck} />);

  await expect(page.getByTestId('player-slide-3')).toHaveAttribute('inert', '');
  for (const at of ITEMS) {
    await expect(item(page, at)).toBeHidden();
  }
  const stops = await tabStops(page);
  expect(stops.filter((stop) => stop.startsWith('reveal-item-'))).toEqual([]);
});
