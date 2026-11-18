import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { guestView } from '../../test-support/fixtures/guestStates';
import { reactionsBurst } from '../../test-support/fixtures/qaStates';
import { deckPlay } from '../../test-support/fixtures/screens';
import { QaScreenProbe } from '../../test-support/QaScreenProbe';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { JoinProvider } from '../../slides/JoinContext';
import { QA_REACTIONS_LAYER_TEST_IDS as IDS } from '../testIds';
import type { ReactionCounts } from '../types';
import { ReactionsLayer } from './ReactionsLayer';
import { ScreenReactionsLive } from './ScreenReactionsLive';
import type { ScreenReactions } from './useScreenReactions';

// The real reactions provider over the stub hub (SW-9 Q7, Q9): reactions events become bursts for a person who can
// present the deck, while the deck is live.
test.use({ viewport: { width: 1920, height: 1080 } });

const deckId = deckPlay.id;
const liveJoin = { ...deckPlay.join, live: true };
const none: ReactionCounts = { thumbsUp: 0, heart: 0, laugh: 0, wow: 0, clap: 0 };

async function stubLive(page: Page, live = true) {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await stubApi(page, {});
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await page.route('**/api/decks/*/live', (request) =>
    request.fulfill({
      json: {
        sessionId: live ? 'session-1' : null,
        code: liveJoin.code,
        codeLabel: liveJoin.codeLabel,
        joinUrl: liveJoin.joinUrl,
        hostLabel: liveJoin.hostLabel,
        live,
        joinedCount: 0,
        joinBar: false,
      },
    }),
  );
  return hub;
}

const screen = (owner: boolean) => (
  <JoinProvider deckId={deckId} join={liveJoin} owner={owner}>
    <ScreenReactionsLive deckId={deckId} owner={owner}>
      <ReactionsLayer />
      <QaScreenProbe />
    </ScreenReactionsLive>
  </JoinProvider>
);

const probed = async (page: Page) =>
  JSON.parse(
    (await page.getByTestId('probe-screen-reactions').textContent()) ?? '',
  ) as ScreenReactions | null;

test('a reactions event floats one emoji per tap, in the order of the phone row', async ({
  mount,
  page,
}) => {
  const hub = await stubLive(page);
  await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);
  expect(hub.watches).toContain(deckId);

  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, clap: 2, heart: 1 } }));

  await expect(page.getByTestId(IDS.float)).toHaveText(['❤️', '👏', '👏']);
  const { bursts } = (await probed(page))!;
  expect(bursts.map((burst) => burst.emoji)).toEqual(['heart', 'clap', 'clap']);
  expect(new Set(bursts.map((burst) => burst.id)).size).toBe(3);
});

test('a second event adds bursts with higher ids', async ({ mount, page }) => {
  const hub = await stubLive(page);
  await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);

  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, wow: 1 } }));
  await expect(page.getByTestId(IDS.float)).toHaveCount(1);
  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, laugh: 1 } }));

  await expect(page.getByTestId(IDS.float)).toHaveText(['😮', '😂']);
});

test('a burst of 44 taps in one event shows 30 and "+14"', async ({ mount, page }) => {
  const hub = await stubLive(page);
  await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);

  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, clap: 44 } }));

  await expect(page.getByTestId(IDS.float)).toHaveCount(30);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+14 👏');
});

test('a viewer who cannot present gets null: no layer, no watch', async ({ mount, page }) => {
  const hub = await stubLive(page);
  await mount(screen(false));

  await expect(page.getByTestId('probe-screen-reactions')).toHaveText('null');
  await expect(page.getByTestId(IDS.layer)).toHaveCount(0);
  await page.evaluate(() => new Promise((done) => setTimeout(done, 300)));
  await expect(page.getByTestId(IDS.layer)).toHaveCount(0);
  expect(hub.watches).toEqual([]);
});

test('a deck that is not live shows no reactions and adds no watch', async ({ mount, page }) => {
  const hub = await stubLive(page, false);
  await mount(screen(true));

  await expect(page.getByTestId(IDS.layer)).toBeAttached();
  await page.evaluate(() => new Promise((done) => setTimeout(done, 300)));
  await expect(page.getByTestId(IDS.float)).toHaveCount(0);
  expect(await probed(page)).toEqual({ bursts: [] });
  expect(hub.watches.length).toBeLessThanOrEqual(1);
});

test('reduced motion turns every tap into the chip', async ({ mount, page }) => {
  const hub = await stubLive(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);

  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, heart: 4 } }));

  await expect(page.getByTestId(IDS.chip)).toHaveText('+4 ❤️');
  await expect(page.getByTestId(IDS.float)).toHaveCount(0);
});

test('the provider drops a burst from its list after a few seconds', async ({ mount, page }) => {
  const hub = await stubLive(page);
  await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);

  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, clap: 2 } }));
  await expect.poll(async () => (await probed(page))!.bursts.length).toBe(2);

  await expect.poll(async () => (await probed(page))!.bursts.length, { timeout: 8000 }).toBe(0);
});
