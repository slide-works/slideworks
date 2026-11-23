import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { sampleDeck } from '../test-support/fixtures/screens';
import { mountPlayer } from '../test-support/mountPlayer';
import { stubApi } from '../test-support/stubApi';
import { JoinProvider } from '../slides/JoinContext';
import { guestView } from '../test-support/fixtures/guestStates';
import { presentationState } from '../test-support/fixtures/presenting';
import { deckPlay } from '../test-support/fixtures/screens';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { Player } from './Player';

type ClipboardWindow = { copiedLinks: string[] };

// Replaces the clipboard before the player mounts: every write is recorded, or
// refused when `refuse` is set, `delayMs` after the write was asked for.
async function stubClipboard(page: Page, refuse: boolean, delayMs = 0) {
  await page.evaluate(
    ([refused, delay]) => {
      const copied: string[] = [];
      (window as unknown as ClipboardWindow).copiedLinks = copied;
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: {
          writeText: (text: string) =>
            new Promise<void>((resolve, reject) =>
              setTimeout(() => {
                if (refused) return reject(new Error('NotAllowedError'));
                copied.push(text);
                resolve();
              }, delay),
            ),
        },
      });
    },
    [refuse, delayMs] as const,
  );
}

function copiedLinks(page: Page) {
  return page.evaluate(() => (window as unknown as ClipboardWindow).copiedLinks);
}

// This page's full address with the given hash.
function linkWith(page: Page, hash: string) {
  const url = new URL(page.url());
  url.hash = hash;
  return url.href;
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
  expect(await copiedLinks(page)).toEqual([linkWith(page, '#3.2')]);
});

test('copy link button writes the next slide link after moving on', async ({ mount, page }) => {
  await stubClipboard(page, false);
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  await page.getByTestId('player-copy-link').click();
  await page.getByTestId('player-next').click();
  await expect(page.getByTestId('player-count')).toHaveText(`2 / ${sampleDeck.slides.length}`);
  await expect(page.getByTestId('player-copy-link-message')).toBeHidden();

  await page.getByTestId('player-copy-link').click();

  await expect.poll(() => copiedLinks(page)).toEqual([linkWith(page, '#1'), linkWith(page, '#2')]);
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

test('copy link button shows a refusal that arrives after the message time', async ({
  mount,
  page,
}) => {
  await stubClipboard(page, true, 3200);
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await page.getByTestId('player-copy-link').click();

  await expect(page.getByTestId('player-copy-link-message')).toBeHidden();
  await expect(page.getByTestId('player-copy-link-message')).toContainText("Couldn't copy", {
    timeout: 4000,
  });
  await expect(page.getByTestId('player-copy-link-message')).toBeHidden({ timeout: 4000 });
});

test('copy link button presses on Space instead of moving the slide', async ({ mount, page }) => {
  await stubClipboard(page, false);
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await page.getByTestId('player-copy-link').focus();
  await page.keyboard.press('Space');

  await expect(page.getByTestId('player-copy-link-message')).toHaveText('Link copied');
  await expect(page.getByTestId('player-count')).toHaveText(`1 / ${sampleDeck.slides.length}`);
  expect(await copiedLinks(page)).toEqual([linkWith(page, '#1')]);
});

test('player Space on a focused control presses it and does not also advance', async ({
  mount,
  page,
}) => {
  await page.evaluate(() => window.history.replaceState(null, '', '#3'));
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await page.getByTestId('player-previous').focus();
  await page.keyboard.press('Space');

  await expect(page.getByTestId('player-count')).toHaveText(`2 / ${sampleDeck.slides.length}`);
});

test('copy link button keeps focus when the keyboard moves the slide', async ({ mount, page }) => {
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await page.getByTestId('player-copy-link').focus();
  await page.keyboard.press('ArrowRight');

  await expect(page.getByTestId('player-count')).toHaveText(`2 / ${sampleDeck.slides.length}`);
  await expect(page.getByTestId('player-copy-link')).toBeFocused();
});

async function expectBarInsideViewport(page: Page) {
  const viewport = page.viewportSize()!;
  const buttons = page.getByTestId('player-controls').locator('button');
  expect(await buttons.count()).toBeGreaterThanOrEqual(4);
  for (const button of await buttons.all()) {
    const box = await button.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.y).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width);
    expect(box!.y + box!.height).toBeLessThanOrEqual(viewport.height);
  }
}

test('player bar keeps every control inside a phone viewport', async ({ mount, page }) => {
  await page.setViewportSize({ width: 360, height: 640 });
  await stubClipboard(page, true);
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await expectBarInsideViewport(page);

  await page.getByTestId('player-copy-link').click();
  const message = await page.getByTestId('player-copy-link-message').boundingBox();
  expect(message!.x).toBeGreaterThanOrEqual(0);
  expect(message!.x + message!.width).toBeLessThanOrEqual(360);
});

test('player bar keeps every control inside a phone viewport while live', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 640 });
  const join = { ...deckPlay.join, live: true };
  await page.route(`**/api/decks/${deckPlay.id}/live`, (request) =>
    request.fulfill({
      json: {
        sessionId: 'session-1',
        code: join.code,
        codeLabel: join.codeLabel,
        joinUrl: join.joinUrl,
        hostLabel: join.hostLabel,
        live: true,
        joinedCount: 3,
        joinBar: false,
      },
    }),
  );
  await stubDeckHub(page, {
    presentation: {
      state: {
        ...presentationState({ deckId: deckPlay.id, slide: 1, step: 0, connected: [] }),
        seq: undefined,
      },
    },
    audience: { state: guestView('live').state },
  });
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner>
      <Player deck={deckPlay} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('live-chip-end')).toBeVisible();

  await expectBarInsideViewport(page);
});

test('player bar keeps every control inside a phone viewport in fullscreen', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 640 });
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await page.getByTestId('player-fullscreen').click();
  await expect(page.getByTestId('player-fullscreen')).toHaveAttribute(
    'aria-label',
    'Exit fullscreen',
  );

  await expectBarInsideViewport(page);
});
