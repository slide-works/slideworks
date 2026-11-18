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

async function stubLive(page: Page, live = true, status = 200) {
  const state = { live };
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await stubApi(page, {});
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await page.route('**/api/decks/*/live', (request) =>
    request.fulfill({
      status,
      json: {
        sessionId: state.live ? 'session-1' : null,
        code: liveJoin.code,
        codeLabel: liveJoin.codeLabel,
        joinUrl: liveJoin.joinUrl,
        hostLabel: liveJoin.hostLabel,
        live: state.live,
        joinedCount: 0,
        joinBar: false,
      },
    }),
  );
  return Object.assign(hub, { setLive: (next: boolean) => void (state.live = next) });
}

const screen = (owner: boolean, join = liveJoin) => (
  <JoinProvider deckId={deckId} join={join} owner={owner}>
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

test('the owner flag alone keeps the screen null: the live session must confirm the viewer', async ({
  mount,
  page,
}) => {
  const hub = await stubLive(page, true, 403);
  await mount(screen(true));

  await page.evaluate(() => new Promise((done) => setTimeout(done, 500)));
  await expect(page.getByTestId('probe-screen-reactions')).toHaveText('null');
  await expect(page.getByTestId(IDS.layer)).toHaveCount(0);
  expect(hub.watches).toEqual([]);
});

test('the join provider saying owner is not enough when this provider says no', async ({
  mount,
  page,
}) => {
  const hub = await stubLive(page);
  await mount(
    <JoinProvider deckId={deckId} join={liveJoin} owner>
      <ScreenReactionsLive deckId={deckId} owner={false}>
        <ReactionsLayer />
        <QaScreenProbe />
      </ScreenReactionsLive>
    </JoinProvider>,
  );

  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(1);
  await page.evaluate(() => new Promise((done) => setTimeout(done, 300)));
  await expect(page.getByTestId('probe-screen-reactions')).toHaveText('null');
  await expect(page.getByTestId(IDS.layer)).toHaveCount(0);
});

test('outside a join provider the screen is null', async ({ mount, page }) => {
  const hub = await stubLive(page);
  await mount(
    <ScreenReactionsLive deckId={deckId} owner>
      <ReactionsLayer />
      <QaScreenProbe />
    </ScreenReactionsLive>,
  );

  await page.evaluate(() => new Promise((done) => setTimeout(done, 300)));
  await expect(page.getByTestId('probe-screen-reactions')).toHaveText('null');
  await expect(page.getByTestId(IDS.layer)).toHaveCount(0);
  expect(hub.watches).toEqual([]);
});

test('a second live session shows its reactions: ids stay above the first session', async ({
  mount,
  page,
}) => {
  const hub = await stubLive(page);
  const component = await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);
  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, clap: 2 } }));
  await expect.poll(async () => (await probed(page))!.bursts.length).toBe(2);
  const first = (await probed(page))!.bursts.map((burst) => burst.id);

  hub.setLive(false);
  await component.update(screen(true, { ...liveJoin, live: false }));
  await expect.poll(async () => (await probed(page))?.bursts.length).toBe(0);

  const watched = hub.watches.length;
  hub.setLive(true);
  await component.update(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThan(watched);
  await page.evaluate(() => new Promise((done) => setTimeout(done, 300)));
  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, wow: 1 } }));

  await expect.poll(async () => (await probed(page))!.bursts.length).toBe(1);
  const second = (await probed(page))!.bursts.map((burst) => burst.id);
  expect(Math.min(...second)).toBeGreaterThan(Math.max(...first));
  await expect(page.getByTestId(IDS.float).filter({ hasText: '😮' })).toHaveCount(1);
});

test('when the live session ends the provider stops listening', async ({ mount, page }) => {
  const hub = await stubLive(page);
  const component = await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);

  hub.setLive(false);
  await component.update(screen(true, { ...liveJoin, live: false }));
  await expect.poll(async () => (await probed(page))?.bursts.length).toBe(0);

  // Events after the end are not turned into bursts.
  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, clap: 3 } }));
  await page.evaluate(() => new Promise((done) => setTimeout(done, 400)));
  expect((await probed(page))!.bursts).toEqual([]);

  // A reconnect makes no watch from this provider: only the live session's own.
  const before = hub.watches.length;
  hub.drop();
  await expect.poll(() => hub.watches.length).toBe(before + 1);
  await page.evaluate(() => new Promise((done) => setTimeout(done, 400)));
  expect(hub.watches).toHaveLength(before + 1);
});

test('a burst leaves the provider list 5 s after it came, not before', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  const hub = await stubLive(page);
  await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);
  const now = await page.evaluate(() => performance.now());
  await page.clock.pauseAt(now + 1000);

  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, clap: 2 } }));
  await expect.poll(async () => (await probed(page))!.bursts.length).toBe(2);

  await page.clock.runFor(4900);
  expect((await probed(page))!.bursts).toHaveLength(2);
  await page.clock.runFor(200);
  await expect.poll(async () => (await probed(page))!.bursts.length).toBe(0);
});

test('a reduced-motion change mid-session applies to the next event', async ({ mount, page }) => {
  const hub = await stubLive(page);
  await mount(screen(true));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);
  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, wow: 1 } }));
  await expect(page.getByTestId(IDS.float)).toHaveCount(1);

  await page.emulateMedia({ reducedMotion: 'reduce' });
  hub.pushAudienceEvent(reactionsBurst({ counts: { ...none, heart: 1 } }));

  await expect(page.getByTestId(IDS.chip)).toHaveText('+1 ❤️');
  await expect(page.getByTestId(IDS.float)).toHaveCount(1);
});
