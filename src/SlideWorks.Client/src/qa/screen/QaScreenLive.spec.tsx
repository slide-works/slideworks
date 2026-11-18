import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { deckPlay } from '../../test-support/fixtures/screens';
import { guestView } from '../../test-support/fixtures/guestStates';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { QaScreenProbe } from '../../test-support/QaScreenProbe';
import { pushQaControl, stubQaControlApi } from '../../test-support/stubQaControlApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { JoinProvider } from '../../slides/JoinContext';
import type { QaControlView } from '../types';
import type { QaScreen } from './QaScreenContext';
import { QaScreenLive } from './QaScreenLive';

// The real screen provider over the stub hub and the stub presenter routes.
const deckId = deckPlay.id;
const liveJoin = { ...deckPlay.join, live: true };

// The sample's live view: question 2 on screen, three open questions. A copy with a seq of the test's choosing.
const view = (overrides: Partial<QaControlView> = {}) => qaControlView('live', overrides);

// GET /live for any deck: the viewer is the owner, and the deck is live while `live` is true.
function liveRoute(page: Page) {
  let live = true;
  const route = page.route('**/api/decks/*/live', async (request) => {
    await request.fulfill({
      json: {
        sessionId: live ? 'session-1' : null,
        code: liveJoin.code,
        codeLabel: liveJoin.codeLabel,
        joinUrl: liveJoin.joinUrl,
        hostLabel: liveJoin.hostLabel,
        live,
        joinedCount: live ? 41 : 0,
        joinBar: false,
      },
    });
  });
  return {
    route,
    setLive(next: boolean) {
      live = next;
    },
  };
}

async function stubLive(page: Page, control: QaControlView = view()) {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  const live = liveRoute(page);
  await live.route;
  const qa = await stubQaControlApi(page, deckId, { control });
  return { hub, qa, live };
}

function screen(owner: boolean, { join = liveJoin, id = deckId } = {}) {
  return (
    <JoinProvider deckId={id} join={join} owner={owner}>
      <QaScreenLive deckId={id} owner={owner}>
        <QaScreenProbe />
      </QaScreenLive>
    </JoinProvider>
  );
}

const read = async (page: Page): Promise<QaScreen | null> =>
  JSON.parse((await page.getByTestId('probe-qa-screen').textContent()) ?? '') as QaScreen | null;

// Lets the page run its pending work, so a call or render it should not make has had its chance.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 300)));

// Both watches are in: the live session's and the screen provider's.
async function watching(hub: Awaited<ReturnType<typeof stubDeckHub>>) {
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);
}

const votesOf = (screen: QaScreen | null) => screen?.top.map((row) => [row.number, row.votes]);

test('a presenter gets the room as the screen shows it from the GET on watch', async ({
  mount,
  page,
}) => {
  const { hub, qa } = await stubLive(page);
  await mount(screen(true));
  await watching(hub);
  expect(hub.watches).toContain(deckId);

  await expect
    .poll(() => read(page))
    .toMatchObject({ phase: 'open', total: 3, showOn: 'anySlide' });
  const shown = await read(page);
  expect(votesOf(shown)).toEqual([
    [1, 14],
    [2, 9],
    [6, 3],
  ]);
  // Hidden, waiting and answered questions are not in the room's list.
  expect(shown?.top.map((row) => row.number)).not.toContain(5);
  expect(shown?.onScreen).toMatchObject({ number: 2, name: null, votes: 9 });
  expect(qa.calls).toEqual([{ call: 'read' }]);
});

test('questionControl moves the screen, and a lower seq is dropped', async ({ mount, page }) => {
  const { hub, qa } = await stubLive(page);
  await mount(screen(true));
  await watching(hub);
  await expect.poll(async () => (await read(page))?.total).toBe(3);
  const base = qa.control().seq;

  const next = view({ seq: base + 10 });
  next.questions[0].votes = 20;
  pushQaControl(hub, next);
  await expect.poll(async () => votesOf(await read(page))?.[0]).toEqual([1, 20]);

  // Late: an older view than the one on screen.
  const late = view({ seq: base + 5 });
  late.questions[0].votes = 1;
  pushQaControl(hub, late);
  await settle(page);
  expect(votesOf(await read(page))?.[0]).toEqual([1, 20]);

  // Equal is not newer either.
  const same = view({ seq: base + 10 });
  same.questions[0].votes = 2;
  pushQaControl(hub, same);
  await settle(page);
  expect(votesOf(await read(page))?.[0]).toEqual([1, 20]);

  // The newest wins again.
  const newest = view({ seq: base + 11 });
  newest.questions[0].votes = 21;
  pushQaControl(hub, newest);
  await expect.poll(async () => votesOf(await read(page))?.[0]).toEqual([1, 21]);
});

test('a read that lands after a newer questionControl is dropped by seq', async ({
  mount,
  page,
}) => {
  const { hub, qa } = await stubLive(page);
  const slow = qa.hold('read');
  await mount(screen(true));
  await watching(hub);
  await expect.poll(() => qa.calls.length).toBe(1);
  const read1 = qa.control().seq;

  const newer = view({ seq: read1 + 10 });
  newer.questions[0].votes = 30;
  pushQaControl(hub, newer);
  await expect.poll(async () => votesOf(await read(page))?.[0]).toEqual([1, 30]);

  slow.release();
  await settle(page);
  expect(votesOf(await read(page))?.[0]).toEqual([1, 30]);
});

test('after a reconnect it watches again and reads again', async ({ mount, page }) => {
  const { hub, qa } = await stubLive(page);
  await mount(screen(true));
  await watching(hub);
  await expect.poll(async () => (await read(page))?.total).toBe(3);
  const watchesBefore = hub.watches.length;

  const changed = view();
  changed.questions[0].votes = 40;
  qa.setControl(changed);
  hub.drop();
  await expect.poll(() => hub.watches.length).toBeGreaterThan(watchesBefore);
  await expect.poll(async () => votesOf(await read(page))?.[0]).toEqual([1, 40]);
  await expect.poll(() => qa.calls.length).toBe(2);
});

test('a viewer who cannot present gets null and makes no call', async ({ mount, page }) => {
  const { hub, qa } = await stubLive(page);
  await mount(screen(false));
  await settle(page);
  expect(await read(page)).toBeNull();
  expect(hub.watches).toEqual([]);
  expect(qa.calls).toEqual([]);
});

test('the owner flag alone keeps the screen null, even when the live session confirms the viewer', async ({
  mount,
  page,
}) => {
  const { hub, qa } = await stubLive(page);
  await mount(
    <JoinProvider deckId={deckId} join={liveJoin} owner>
      <QaScreenLive deckId={deckId} owner={false}>
        <QaScreenProbe />
      </QaScreenLive>
    </JoinProvider>,
  );
  // The live session's own watch is in, so the session is confirmed; the screen provider still makes no call.
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(1);
  await settle(page);
  expect(await read(page)).toBeNull();
  expect(qa.calls).toEqual([]);
  expect(hub.watches).toHaveLength(1);
});

test('a deck that is not live gets null and makes no call, then reads once it goes live', async ({
  mount,
  page,
}) => {
  const { hub, qa, live } = await stubLive(page);
  live.setLive(false);
  const component = await mount(screen(true, { join: { ...liveJoin, live: false } }));
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(1);
  await settle(page);
  expect(await read(page)).toBeNull();
  expect(qa.calls).toEqual([]);

  live.setLive(true);
  await component.update(screen(true));
  await expect.poll(async () => (await read(page))?.total).toBe(3);
  expect(qa.calls).toEqual([{ call: 'read' }]);
});

test('questions off has no round, so the screen is null', async ({ mount, page }) => {
  const off = view({ round: null, onScreen: null, questions: [] });
  off.counts = { open: 0, waiting: 0, answered: 0, hidden: 0 };
  off.settings = { ...off.settings, questions: 'off' };
  const { hub, qa } = await stubLive(page, off);
  await mount(screen(true));
  await watching(hub);
  await expect.poll(() => qa.calls.length).toBe(1);
  await settle(page);
  expect(await read(page)).toBeNull();
});

test('a round that appears later shows, and one that ends goes back to null', async ({
  mount,
  page,
}) => {
  const { hub, qa } = await stubLive(page);
  await mount(screen(true));
  await watching(hub);
  await expect.poll(async () => (await read(page))?.total).toBe(3);

  const base = qa.control().seq;
  pushQaControl(hub, view({ seq: base + 1, round: null, questions: [], onScreen: null }));
  await expect.poll(() => read(page)).toBeNull();
  pushQaControl(hub, view({ seq: base + 2 }));
  await expect.poll(async () => (await read(page))?.total).toBe(3);
});

test('closed settings give the closed phase', async ({ mount, page }) => {
  const closed = view();
  closed.settings = { ...closed.settings, questions: 'closed' };
  const { hub } = await stubLive(page, closed);
  await mount(screen(true));
  await watching(hub);
  await expect.poll(async () => (await read(page))?.phase).toBe('closed');
});

test('a new deck starts with no view of the last one', async ({ mount, page }) => {
  const { hub } = await stubLive(page);
  const component = await mount(screen(true));
  await watching(hub);
  await expect.poll(async () => (await read(page))?.total).toBe(3);

  await component.update(screen(true, { id: 'otherDeck1' }));
  await expect.poll(() => hub.watches).toContain('otherDeck1');
  await expect.poll(() => read(page)).toBeNull();
});

test('a new live session shows no view of the last one', async ({ mount, page }) => {
  const { hub, qa, live } = await stubLive(page);
  const component = await mount(screen(true));
  await watching(hub);
  await expect.poll(async () => (await read(page))?.total).toBe(3);

  live.setLive(false);
  await component.update(screen(true, { join: { ...liveJoin, live: false } }));
  await expect.poll(() => read(page)).toBeNull();

  // Live again: the old view does not show while the new read is out.
  qa.setLive(true);
  const slow = qa.hold('read');
  live.setLive(true);
  await component.update(screen(true));
  await expect.poll(() => qa.calls.length).toBeGreaterThanOrEqual(2);
  await settle(page);
  expect(await read(page)).toBeNull();
  slow.release();
  await expect.poll(async () => (await read(page))?.total).toBe(0);
});
