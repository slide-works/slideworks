import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { sampleDeck } from '../test-support/fixtures/screens';
import { mountPlayer } from '../test-support/mountPlayer';
import { stubApi } from '../test-support/stubApi';
import { Player } from './Player';

type ClipboardWindow = { copiedLinks: string[] };

// Replaces the clipboard before the player mounts: every write is recorded, or
// refused when `refuse` is set.
async function stubClipboard(page: Page, refuse: boolean) {
  await page.evaluate((refused) => {
    const copied: string[] = [];
    (window as unknown as ClipboardWindow).copiedLinks = copied;
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: (text: string) => {
          if (refused) return Promise.reject(new Error('NotAllowedError'));
          copied.push(text);
          return Promise.resolve();
        },
      },
    });
  }, refuse);
}

function copiedLinks(page: Page) {
  return page.evaluate(() => (window as unknown as ClipboardWindow).copiedLinks);
}

test.beforeEach(async ({ page }) => {
  await stubApi(page, {});
  await page.evaluate(() => window.history.replaceState(null, '', '#1'));
});

test('copy link button writes the current slide link with its hash', async ({ mount, page }) => {
  await stubClipboard(page, false);
  await page.evaluate(() => window.history.replaceState(null, '', '#3.2'));
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await page.getByTestId('player-copy-link').click();

  await expect(page.getByTestId('player-copy-link-message')).toHaveText('Link copied');
  expect((await copiedLinks(page)).map((link) => link.slice(link.indexOf('#')))).toEqual(['#3.2']);
});

test('copy link button writes the next slide link after moving on', async ({ mount, page }) => {
  await stubClipboard(page, false);
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  await page.getByTestId('player-copy-link').click();
  await page.getByTestId('player-next').click();
  await expect(page.getByTestId('player-count')).toHaveText(`2 / ${sampleDeck.slides.length}`);
  await expect(page.getByTestId('player-copy-link-message')).toBeHidden();

  await page.getByTestId('player-copy-link').click();

  await expect
    .poll(async () => (await copiedLinks(page)).map((link) => link.slice(link.indexOf('#'))))
    .toEqual(['#1', '#2']);
});

test('copy link button shows a message when the clipboard refuses', async ({ mount, page }) => {
  await stubClipboard(page, true);
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await page.getByTestId('player-copy-link').click();

  await expect(page.getByTestId('player-copy-link-message')).toContainText("Couldn't copy");
  await expect(page.getByTestId('player-copy-link-message')).toHaveAttribute('role', 'alert');
});

test('copy link button is followed by fullscreen in Tab order', async ({ mount, page }) => {
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await page.getByTestId('player-copy-link').focus();
  await page.keyboard.press('Tab');

  await expect(page.getByTestId('player-fullscreen')).toBeFocused();
});
