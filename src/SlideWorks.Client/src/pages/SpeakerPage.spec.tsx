import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../AppRoutes';
import type { PresentationState } from '../presentation/types';
import { presentationState, presenterView } from '../test-support/fixtures/presenting';
import { me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub, type StubPresentationOptions } from '../test-support/stubDeckHub';

const view = presenterView();
const deckId = view.id;
const route = { hooksConfig: { route: `/remote/${deckId}` } };
const presenterPath = `/api/decks/${deckId}/presenter`;

// A paused timer, so nothing on the page moves on its own.
const paused = (overrides: Partial<PresentationState> = {}) =>
  presentationState({ timer: { state: 'paused', elapsedMs: 760000 }, ...overrides });

test.beforeEach(async ({ page }) => {
  await page.route(`**${view.look.logo!.url}`, (r) =>
    r.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
});

// The owner on their phone: the presenter view answers and the hub plays the presentation.
async function open(page: Page, presentation: StubPresentationOptions, presenterStatus = 200) {
  await stubApi(page, {
    '/api/me': { json: me },
    [presenterPath]:
      presenterStatus === 200
        ? { json: view }
        : { status: presenterStatus, json: { error: 'Deck not found' } },
  });
  return stubDeckHub(page, { presentation });
}

const live = (page: Page) =>
  expect(page.getByTestId('speaker-status')).toHaveAttribute('data-status', 'live');

test('speaker mode joins as a phone and shows the slide, the next row and the notes', async ({
  mount,
  page,
}) => {
  const hub = await open(page, { state: paused() });
  await mount(<AppRoutes />, route);

  await live(page);
  expect(hub.presentationJoins).toHaveLength(1);
  expect(hub.presentationJoins[0]).toMatchObject({ deckId, role: 'phone' });
  expect(hub.presentationJoins[0].label).not.toBe('');
  await expect(page.getByTestId('speaker-count')).toHaveText('8 / 16');
  await expect(page.getByTestId('speaker-part')).toHaveText('Slice it');
  await expect(page.getByTestId('speaker-now')).toHaveAttribute('data-slide', '8');
  await expect(page.getByTestId('speaker-next-label')).toHaveText('slide 9');
  await expect(page.getByTestId('speaker-next-note')).toHaveText('2 deep dives ahead');
  await expect(page.getByTestId('speaker-notes')).toContainText('Keep each slice under a week');
  await expect(page.getByTestId('speaker-time-left')).toHaveText('17:20 left');
  await expect(page.getByTestId('speaker-next')).toHaveAccessibleName(
    'Next: slide 9: The difference is when the customer gets to answer',
  );
  await expect(page.getByTestId('speaker-previous')).toHaveAccessibleName(
    'Previous: slide 7: Slice it thin',
  );
});

test('Next and Previous send the positions navigate works out', async ({ mount, page }) => {
  // Slide 3 has 4 reveals.
  const hub = await open(page, { state: paused({ slide: 3, step: 0 }) });
  await mount(<AppRoutes />, route);
  await live(page);
  await expect(page.getByTestId('speaker-next-label')).toHaveText('reveal 1 of 4');

  await page.getByTestId('speaker-next').click();
  await expect(page.getByTestId('speaker-now')).toHaveAttribute('data-step', '1');
  expect(hub.updates).toEqual([{ slide: 3, step: 1, black: false, skipDeepDives: false }]);
  await expect(page.getByTestId('speaker-notes-step')).toHaveText('· reveal 1 of 4');

  // From slide 4, Previous lands on slide 3 with all its steps shown (K12).
  hub.pushState(paused({ seq: 50, slide: 4, step: 0 }));
  await expect(page.getByTestId('speaker-now')).toHaveAttribute('data-slide', '4');
  await page.getByTestId('speaker-previous').click();
  await expect(page.getByTestId('speaker-now')).toHaveAttribute('data-slide', '3');
  expect(hub.updates[1]).toEqual({ slide: 3, step: 4, black: false, skipDeepDives: false });
});

test('Skip deep dives turns skipping on, and Next then steps over the deep dive', async ({
  mount,
  page,
}) => {
  const hub = await open(page, { state: paused({ slide: 9 }) });
  await mount(<AppRoutes />, route);
  await live(page);
  await expect(page.getByTestId('speaker-skip')).toHaveAttribute('aria-pressed', 'false');
  await expect(page.getByTestId('speaker-next-label')).toHaveText('slide 10');

  await page.getByTestId('speaker-skip').click();
  await expect(page.getByTestId('speaker-skip')).toHaveAttribute('aria-pressed', 'true');
  expect(hub.updates).toEqual([{ slide: 9, step: 0, black: false, skipDeepDives: true }]);
  await expect(page.getByTestId('speaker-next-label')).toHaveText('slide 11');
  await expect(page.getByTestId('speaker-next-note')).toHaveText('skipping deep dive 10');

  await page.getByTestId('speaker-next').click();
  await expect(page.getByTestId('speaker-count')).toHaveText('11 / 16');
  expect(hub.updates[1]).toEqual({ slide: 11, step: 0, black: false, skipDeepDives: true });
});

test('Blackout sends black and says the room sees a black screen', async ({ mount, page }) => {
  const hub = await open(page, { state: paused() });
  await mount(<AppRoutes />, route);
  await live(page);
  await expect(page.getByTestId('speaker-black-notice')).toHaveCount(0);

  await page.getByTestId('speaker-blackout').click();
  await expect(page.getByTestId('speaker-blackout')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId('speaker-black-notice')).toHaveText('The room sees a black screen');
  expect(hub.updates).toEqual([{ slide: 8, step: 0, black: true, skipDeepDives: false }]);

  await page.getByTestId('speaker-blackout').click();
  await expect(page.getByTestId('speaker-black-notice')).toHaveCount(0);
  expect(hub.updates[1]).toEqual({ slide: 8, step: 0, black: false, skipDeepDives: false });
});

test('Slides opens the sheet, and a row jumps there and closes it', async ({ mount, page }) => {
  const hub = await open(page, { state: paused() });
  await mount(<AppRoutes />, route);
  await live(page);

  await page.getByTestId('speaker-slides').click();
  await expect(page.getByTestId('slides-sheet')).toBeVisible();
  await page.getByTestId('slides-sheet-row-12').click();

  await expect(page.getByTestId('slides-sheet')).toHaveCount(0);
  await expect(page.getByTestId('speaker-count')).toHaveText('12 / 16');
  expect(hub.updates).toEqual([{ slide: 12, step: 0, black: false, skipDeepDives: false }]);
  await expect(page.getByTestId('speaker-next')).toBeFocused();
});

test('closing the sheet without a jump sends nothing and puts focus back on Slides', async ({
  mount,
  page,
}) => {
  const hub = await open(page, { state: paused() });
  await mount(<AppRoutes />, route);
  await live(page);

  await page.getByTestId('speaker-slides').click();
  await page.getByRole('button', { name: 'Close slides' }).click();

  await expect(page.getByTestId('slides-sheet')).toHaveCount(0);
  await expect(page.getByTestId('speaker-slides')).toBeFocused();
  expect(hub.updates).toEqual([]);
});

test('reconnecting disables the buttons, keeps the notes, and sends nothing', async ({
  mount,
  page,
}) => {
  const hub = await open(page, { state: paused({ slide: 12 }), holdJoin: true });
  await mount(<AppRoutes />, route);
  await expect.poll(() => hub.presentationJoins.length).toBe(1);
  hub.releasePresentationJoins();
  await live(page);
  await page.getByTestId('speaker-next').focus();

  hub.drop();

  await expect(page.getByTestId('speaker-reconnecting')).toBeVisible();
  await expect(page.getByTestId('speaker-reconnecting')).toContainText('stays on slide 12');
  await expect(page.getByTestId('speaker-reconnecting')).toBeFocused();
  for (const id of [
    'speaker-next',
    'speaker-previous',
    'speaker-slides',
    'speaker-skip',
    'speaker-blackout',
    'speaker-timer',
  ]) {
    await expect(page.getByTestId(id)).toBeDisabled();
  }
  await expect(page.getByTestId('speaker-notes')).toContainText('Notes for slide 12 (last known)');
  await expect(page.getByTestId('speaker-notes')).toContainText('Lead time');
  await page.getByTestId('speaker-next').click({ force: true });

  await expect.poll(() => hub.presentationJoins.length).toBe(2);
  hub.releasePresentationJoins();
  await live(page);
  await expect(page.getByTestId('speaker-reconnecting')).toHaveCount(0);
  await expect(page.getByTestId('speaker-next')).toBeEnabled();
  await expect(page.getByTestId('speaker-next')).toBeFocused();
  expect(hub.updates).toEqual([]);
});

test('Next at the end of the deck hands focus to Previous', async ({ mount, page }) => {
  const hub = await open(page, { state: paused({ slide: 15 }) });
  await mount(<AppRoutes />, route);
  await live(page);
  await page.getByTestId('speaker-next').focus();

  await page.keyboard.press('Enter');

  await expect(page.getByTestId('speaker-count')).toHaveText('16 / 16');
  expect(hub.updates).toEqual([{ slide: 16, step: 0, black: false, skipDeepDives: false }]);
  await expect(page.getByTestId('speaker-next')).toBeDisabled();
  await expect(page.getByTestId('speaker-next')).toHaveAccessibleName(
    'Next: at the end of the deck',
  );
  await expect(page.getByTestId('speaker-previous')).toBeFocused();
});

test('an open sheet hides while reconnecting and takes focus back when live again', async ({
  mount,
  page,
}) => {
  const hub = await open(page, { state: paused(), holdJoin: true });
  await mount(<AppRoutes />, route);
  await expect.poll(() => hub.presentationJoins.length).toBe(1);
  hub.releasePresentationJoins();
  await live(page);
  await page.getByTestId('speaker-slides').click();
  await expect(page.getByTestId('slides-sheet')).toBeVisible();

  hub.drop();

  await expect(page.getByTestId('slides-sheet')).toHaveCount(0);
  await expect(page.getByTestId('speaker-reconnecting')).toBeFocused();
  await expect.poll(() => hub.presentationJoins.length).toBe(2);
  hub.releasePresentationJoins();
  await expect(page.getByTestId('slides-sheet')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Close slides' })).toBeFocused();
  expect(hub.updates).toEqual([]);
});

test('a 404 on the presenter view says the presentation is not yours', async ({ mount, page }) => {
  await open(page, { state: paused() }, 404);
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('speaker-not-yours')).toHaveText(
    "This presentation isn't yours, or it doesn't exist",
  );
  await expect(page.getByTestId('speaker-next')).toHaveCount(0);
});

test('a refused join says the presentation is not yours', async ({ mount, page }) => {
  const hub = await open(page, { state: paused(), refuse: true });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('speaker-not-yours')).toHaveText(
    "This presentation isn't yours, or it doesn't exist",
  );
  expect(hub.presentationJoins).toHaveLength(1);
  await expect(page.getByTestId('speaker-next')).toHaveCount(0);
});

test('a failed update shows a message and the slide stays', async ({ mount, page }) => {
  const hub = await open(page, { state: paused() });
  await mount(<AppRoutes />, route);
  await live(page);
  hub.fail('UpdatePresentation', 'Not presenting');

  await page.getByTestId('speaker-next').click();

  await expect(page.getByTestId('speaker-error')).toHaveText(
    "That didn't reach the talk, so nothing changed. Try again.",
  );
  await expect(page.getByTestId('speaker-count')).toHaveText('8 / 16');

  await page.getByTestId('speaker-next').click();
  await expect(page.getByTestId('speaker-count')).toHaveText('9 / 16');
  await expect(page.getByTestId('speaker-error')).toHaveCount(0);
});

test('a failed timer call shows a message and the timer stays as it was', async ({
  mount,
  page,
}) => {
  const hub = await open(page, { state: presentationState() });
  await mount(<AppRoutes />, route);
  await live(page);
  hub.fail('SetTimer', 'Not presenting');

  await page.getByTestId('speaker-timer').click();

  await expect(page.getByTestId('speaker-error')).toHaveText(
    "That didn't reach the talk, so nothing changed. Try again.",
  );
  await expect(page.getByTestId('speaker-timer')).toHaveAttribute('data-state', 'running');
  expect(hub.timers).toEqual(['pause']);
});

test('a presenter view that fails to load says so, and Try again loads it', async ({
  mount,
  page,
}) => {
  let calls = 0;
  await stubApi(page, { '/api/me': { json: me } });
  await page.route(`**${presenterPath}`, (r) => {
    calls += 1;
    return calls === 1
      ? r.fulfill({ status: 500, json: { error: 'Something went wrong.' } })
      : r.fulfill({ json: view });
  });
  await stubDeckHub(page, { presentation: { state: paused() } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('speaker-load-failed')).toContainText("Couldn't load this talk.");
  await page.getByTestId('speaker-load-retry').click();

  await live(page);
  await expect(page.getByTestId('speaker-count')).toHaveText('8 / 16');
});

test('tapping the timer pauses it, and tapping again resumes it', async ({ mount, page }) => {
  const hub = await open(page, { state: presentationState() });
  await mount(<AppRoutes />, route);
  await live(page);
  await expect(page.getByTestId('speaker-timer')).toHaveAttribute('data-state', 'running');

  await page.getByTestId('speaker-timer').click();
  await expect(page.getByTestId('speaker-timer')).toHaveAttribute('data-state', 'paused');
  await page.getByTestId('speaker-timer').click();
  await expect(page.getByTestId('speaker-timer')).toHaveAttribute('data-state', 'running');
  expect(hub.timers).toEqual(['pause', 'resume']);
});

test('the timer turns amber with 5 minutes left and red once over', async ({ mount, page }) => {
  const hub = await open(page, {
    state: paused({ timer: { state: 'paused', elapsedMs: 1_560_000 } }),
  });
  await mount(<AppRoutes />, route);
  await live(page);
  await expect(page.getByTestId('speaker-timer')).toHaveAttribute('data-tone', 'amber');
  await expect(page.getByTestId('speaker-time-left')).toHaveText('4:00 left');

  hub.pushState(paused({ seq: 50, timer: { state: 'paused', elapsedMs: 1_842_000 } }));
  await expect(page.getByTestId('speaker-timer')).toHaveAttribute('data-tone', 'red');
  await expect(page.getByTestId('speaker-time-left')).toHaveText('0:42 over');
});

// A fake Screen Wake Lock that counts what the page asks for and lets go of.
async function stubWakeLock(page: Page) {
  await page.addInitScript(() => {
    const counts = { requests: 0, releases: 0 };
    Object.assign(window, { wakeLockCounts: counts });
    Object.defineProperty(navigator, 'wakeLock', {
      configurable: true,
      value: {
        request: async (type: string) => {
          counts.requests += 1;
          const sentinel = Object.assign(new EventTarget(), {
            type,
            released: false,
            release: async () => {
              if (sentinel.released) return;
              sentinel.released = true;
              counts.releases += 1;
              sentinel.dispatchEvent(new Event('release'));
            },
          });
          return sentinel;
        },
      },
    });
  });
  return () =>
    page.evaluate(() => (window as unknown as { wakeLockCounts: object }).wakeLockCounts);
}

test('the screen wake lock is held while live and released when the connection drops', async ({
  mount,
  page,
}) => {
  const counts = await stubWakeLock(page);
  const hub = await open(page, { state: paused(), holdJoin: true });
  await mount(<AppRoutes />, route);
  await expect.poll(() => hub.presentationJoins.length).toBe(1);
  await expect.poll(counts).toEqual({ requests: 0, releases: 0 });
  hub.releasePresentationJoins();
  await live(page);
  await expect.poll(counts).toEqual({ requests: 1, releases: 0 });

  hub.drop();

  await expect(page.getByTestId('speaker-reconnecting')).toBeVisible();
  await expect.poll(counts).toEqual({ requests: 1, releases: 1 });
  await expect.poll(() => hub.presentationJoins.length).toBe(2);
  hub.releasePresentationJoins();
  await live(page);
  await expect.poll(counts).toEqual({ requests: 2, releases: 1 });
});

test('speaker mode without a wake lock still runs', async ({ mount, page }) => {
  await page.addInitScript(() =>
    Object.defineProperty(navigator, 'wakeLock', { configurable: true, value: undefined }),
  );
  await open(page, { state: paused() });
  await mount(<AppRoutes />, route);
  await live(page);
  await expect(page.getByTestId('speaker-next')).toBeEnabled();
});

test('speaker mode screenshot at phone size', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await open(page, { state: paused() });
  await mount(<AppRoutes />, route);
  await live(page);
  await expect(page.getByTestId('speaker-page')).toHaveScreenshot('speaker-mode.png');
});

test('a failed re-read after the deck goes live or ends shows a message, and the next one clears it', async ({
  mount,
  page,
}) => {
  let failing = false;
  await stubApi(page, { '/api/me': { json: me } });
  await page.route(`**${presenterPath}`, (r) =>
    failing ? r.fulfill({ status: 500, json: { error: 'boom' } }) : r.fulfill({ json: view }),
  );
  const hub = await stubDeckHub(page, { presentation: { state: paused() } });
  await mount(<AppRoutes />, route);
  await live(page);
  await expect(page.getByTestId('speaker-error')).toHaveCount(0);

  failing = true;
  hub.push({ type: 'liveChanged', deckId });

  await expect(page.getByTestId('speaker-error')).toHaveText(
    "Couldn't load the deck's latest changes.",
  );
  await expect(page.getByTestId('speaker-count')).toHaveText('8 / 16');

  failing = false;
  hub.push({ type: 'liveChanged', deckId });
  await expect(page.getByTestId('speaker-error')).toHaveCount(0);
});
