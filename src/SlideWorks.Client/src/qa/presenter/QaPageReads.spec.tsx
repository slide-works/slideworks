import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import { guestView } from '../../test-support/fixtures/guestStates';
import { presentationState, presenterView } from '../../test-support/fixtures/presenting';
import { me } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { stubPollControlApi } from '../../test-support/stubPollControlApi';
import { stubQaControlApi } from '../../test-support/stubQaControlApi';

// SW-9 Q6: nothing publishes questionControl when the presenter moves, so the presenter window and the phone remote each
// read GET /api/decks/{id}/qa once more for every move, through the real QaControlLive and SW-4's presentation state.

const view = presenterView();
const deckId = view.id;

// Each state is newer than the last, as the hub numbers them; the client drops one that isn't.
let seq = 1000;
const paused = (slide: number, step = 0) =>
  presentationState({ seq: ++seq, slide, step, timer: { state: 'paused', elapsedMs: 760000 } });

test.beforeEach(async ({ page }) => {
  await page.route(`**${view.look.logo!.url}`, (r) =>
    r.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
});

async function open(
  page: Page,
  mount: (c: React.JSX.Element, o: { hooksConfig: { route: string } }) => Promise<unknown>,
  path: string,
) {
  const hub = await stubDeckHub(page, {
    presentation: { state: paused(8) },
    audience: { state: guestView('live').state },
  });
  await stubApi(page, {
    '/api/me': { json: me },
    [`/api/decks/${deckId}/presenter`]: { json: view },
  });
  await stubPollControlApi(page, deckId, { polls: [] });
  const api = await stubQaControlApi(page, deckId);
  await mount(<AppRoutes />, { hooksConfig: { route: path } });
  return { hub, api };
}

const reads = (api: Awaited<ReturnType<typeof stubQaControlApi>>) =>
  api.calls.filter((c) => c.call === 'read').length;

test('PresenterPage_WhenThePresenterMoves_ShouldReadTheQuestionsOnceMorePerMove', async ({
  mount,
  page,
}) => {
  const { hub, api } = await open(page, mount, `/decks/${deckId}/present`);
  await expect(page.getByTestId('presenter-count')).toHaveText('8 / 16');
  await expect.poll(() => reads(api)).toBe(1);

  hub.pushState(paused(9));
  await expect(page.getByTestId('presenter-count')).toHaveText('9 / 16');
  await expect.poll(() => reads(api)).toBe(2);

  hub.pushState(paused(10));
  await expect(page.getByTestId('presenter-count')).toHaveText('10 / 16');
  await expect.poll(() => reads(api)).toBe(3);

  // The same slide again (a timer tick, a step) is not a move.
  hub.pushState(paused(10, 1));
  await page.evaluate(() => new Promise((done) => setTimeout(done, 300)));
  expect(reads(api)).toBe(3);
});

test('SpeakerPage_WhenThePresenterMoves_ShouldReadTheQuestionsOnceMorePerMove', async ({
  mount,
  page,
}) => {
  const { hub, api } = await open(page, mount, `/remote/${deckId}`);
  await expect(page.getByTestId('speaker-status')).toHaveAttribute('data-status', 'live');
  await expect.poll(() => reads(api)).toBe(1);

  hub.pushState(paused(9));
  await expect.poll(() => reads(api)).toBe(2);
  hub.pushState(paused(10));
  await expect.poll(() => reads(api)).toBe(3);
});
