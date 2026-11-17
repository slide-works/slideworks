import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { presentationState } from '../test-support/fixtures/presenting';
import { PresentationHarness } from '../test-support/PresentationHarness';
import { stubDeckHub } from '../test-support/stubDeckHub';
import type { PresentationState } from './types';

const deckId = '7Kq2abcdEFGH';
const paused = (overrides: Partial<PresentationState> = {}) =>
  presentationState({ timer: { state: 'paused', elapsedMs: 760000 }, ...overrides });

const status = (page: Page) => page.getByTestId('status');
const position = (page: Page) => page.getByTestId('position');

// Lets the page run its pending work, so a call it should not make has had its chance.
async function settle(page: Page) {
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 300)));
}

test('usePresentation joins with its client id, role, label and time zone, and is live after the join', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  await mount(<PresentationHarness deckId={deckId} role="phone" label="Safari on iPhone" />);

  await expect(status(page)).toHaveText('live');
  await expect(position(page)).toHaveText('8.0');
  await expect(page.getByTestId('clock')).toHaveText('14:32');
  const timeZone = await page.evaluate(() => Intl.DateTimeFormat().resolvedOptions().timeZone);
  expect(hub.presentationJoins).toHaveLength(1);
  const [join] = hub.presentationJoins;
  expect(join).toMatchObject({
    deckId,
    role: 'phone',
    label: 'Safari on iPhone',
    timeZone,
    resume: null,
  });
  expect(join.clientId).toMatch(/^[0-9a-f-]{32,36}$/);
});

test('usePresentation is refused on a refused join and does not try again', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, {
    presentation: { state: presentationState(), refuse: true },
  });
  await mount(<PresentationHarness deckId={deckId} />);

  await expect(status(page)).toHaveText('refused');
  await expect(position(page)).toHaveText('');

  // Even a reconnect does not ask again.
  hub.drop();
  await expect.poll(() => hub.sockets).toBe(2);
  await settle(page);
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
  await settle(page);
  expect(hub.presentationJoins).toHaveLength(1);
  await expect(status(page)).toHaveText('refused');
});

test('usePresentation drops a stale seq within an epoch and applies a new epoch', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect(page.getByTestId('version')).toHaveText('p-3f9c2a/42');

  hub.pushState(presentationState({ seq: 41, slide: 2 }));
  hub.pushState(presentationState({ seq: 42, slide: 3 }));
  hub.pushState(presentationState({ seq: 43, slide: 9 }));
  await expect(position(page)).toHaveText('9.0');
  hub.pushState(presentationState({ seq: 40, slide: 4 }));
  await settle(page);
  await expect(position(page)).toHaveText('9.0');

  // A restarted server starts a new epoch, with a lower seq.
  hub.pushState(presentationState({ epoch: 'p-77aa01', seq: 1, slide: 5 }));
  await expect(page.getByTestId('version')).toHaveText('p-77aa01/1');
  await expect(position(page)).toHaveText('5.0');
});

test('usePresentation ignores a late join result older than a broadcast it has applied', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, {
    presentation: { state: presentationState(), holdJoin: true },
  });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect.poll(() => hub.presentationJoins.length).toBe(1);

  hub.pushState(presentationState({ seq: 50, slide: 10 }));
  await expect(position(page)).toHaveText('10.0');
  await expect(status(page)).toHaveText('connecting');
  hub.releasePresentationJoins();

  await expect(status(page)).toHaveText('live');
  await settle(page);
  await expect(page.getByTestId('version')).toHaveText('p-3f9c2a/50');
  await expect(position(page)).toHaveText('10.0');
});

test('usePresentation applies a join result from a new epoch over an applied broadcast', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, {
    presentation: {
      state: presentationState({ epoch: 'p-new001', seq: 1, slide: 3 }),
      holdJoin: true,
    },
  });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect.poll(() => hub.presentationJoins.length).toBe(1);

  hub.pushState(presentationState({ seq: 50, slide: 10 }));
  await expect(position(page)).toHaveText('10.0');
  hub.releasePresentationJoins();

  await expect(page.getByTestId('version')).toHaveText('p-new001/1');
  await expect(position(page)).toHaveText('3.0');
});

test('usePresentation update sends UpdatePresentation and shows the state it gets back', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect(status(page)).toHaveText('live');

  await page.getByTestId('next').click();

  await expect(position(page)).toHaveText('9.0');
  expect(hub.updates).toEqual([{ slide: 9, step: 0, black: false, skipDeepDives: false }]);
});

test('usePresentation update does nothing until it is live', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, {
    presentation: { state: presentationState(), holdJoin: true },
  });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect.poll(() => hub.presentationJoins.length).toBe(1);
  hub.pushState(presentationState({ seq: 43 }));
  await expect(position(page)).toHaveText('8.0');

  await page.getByTestId('next').click();
  await settle(page);

  expect(hub.updates).toEqual([]);
  await expect(position(page)).toHaveText('8.0');
});

test('usePresentation timer pause sends SetTimer', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect(status(page)).toHaveText('live');

  await page.getByTestId('pause').click();

  await expect(page.getByTestId('timer')).toHaveText('paused');
  expect(hub.timers).toEqual(['pause']);
});

test('usePresentation elapsed advances while the timer runs and not while it is paused', async ({
  mount,
  page,
}) => {
  await page.clock.install();
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect(status(page)).toHaveText('live');
  await expect(page.getByTestId('elapsed')).toHaveText(/^12:4[01]$/);

  await page.clock.runFor(5000);
  await expect(page.getByTestId('elapsed')).toHaveText(/^12:4[56]$/);

  hub.pushState(paused({ seq: 43 }));
  await expect(page.getByTestId('elapsed')).toHaveText('12:40');
  await page.clock.runFor(5000);
  await expect(page.getByTestId('elapsed')).toHaveText('12:40');
});

test('usePresentation shows reconnecting after a drop, then rejoins resuming from the last state', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { presentation: { state: paused(), holdJoin: true } });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect.poll(() => hub.presentationJoins.length).toBe(1);
  hub.releasePresentationJoins();
  await expect(status(page)).toHaveText('live');
  hub.pushState(paused({ seq: 43, slide: 10, step: 1, black: true, skipDeepDives: true }));
  await expect(position(page)).toHaveText('10.1');

  hub.drop();

  await expect(status(page)).toHaveText('reconnecting');
  await expect.poll(() => hub.presentationJoins.length).toBe(2);
  expect(hub.presentationJoins[1].resume).toEqual({
    slide: 10,
    step: 1,
    black: true,
    skipDeepDives: true,
    timer: { state: 'paused', elapsedMs: 760000 },
  });
  expect(hub.presentationJoins[1].clientId).toBe(hub.presentationJoins[0].clientId);
  hub.releasePresentationJoins();
  await expect(status(page)).toHaveText('live');
});

test('usePresentation resumes a running timer from the time it shows', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect(status(page)).toHaveText('live');

  hub.drop();

  await expect.poll(() => hub.presentationJoins.length).toBe(2);
  const resume = hub.presentationJoins[1].resume as { timer: { state: string; elapsedMs: number } };
  expect(resume.timer.state).toBe('running');
  expect(resume.timer.elapsedMs).toBeGreaterThanOrEqual(760000);
});

test('usePresentation rejoins when the page becomes visible again', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  await mount(<PresentationHarness deckId={deckId} />);
  await expect(status(page)).toHaveText('live');

  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));

  await expect.poll(() => hub.presentationJoins.length).toBe(2);
  expect(hub.presentationJoins[1].resume).toMatchObject({ slide: 8, step: 0 });
  await expect(status(page)).toHaveText('live');
});

test('usePresentation shares one WebSocket with useDeckRealtime on the same page', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  await mount(<PresentationHarness deckId={deckId} followDeck />);

  await expect(status(page)).toHaveText('live');
  await expect.poll(() => hub.joined).toEqual([deckId]);
  await settle(page);
  expect(hub.sockets).toBe(1);
  expect(hub.presentationJoins).toHaveLength(1);
});
