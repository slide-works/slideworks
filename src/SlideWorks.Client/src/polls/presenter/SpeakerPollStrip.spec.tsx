import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import { guestView } from '../../test-support/fixtures/guestStates';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import { presentationState, presenterView } from '../../test-support/fixtures/presenting';
import { me } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { pushPollControl, stubPollControlApi } from '../../test-support/stubPollControlApi';
import type { PollControlView } from '../types';
import { POLL_STRIP_TEST_IDS as STRIP } from '../testIds';

// SW-4's phone remote (SpeakerPage) with the real poll control provider and SW-278's strip, at small phone sizes: the
// strip must never push Next or Previous off screen or change their size, in any phase or with an error showing, and on
// a slide with no poll the page must be exactly as it was. Each test measures the page before the poll is read (the
// read is held, so the strip is not there yet: the page as SW-4 drew it) and again after.

const view = presenterView();
const deckId = view.id;
const route = { hooksConfig: { route: `/remote/${deckId}` } };
const presenterPath = `/api/decks/${deckId}/presenter`;
// The fixture deck's slide 8 has no poll; the poll is put on slide 9.
const NO_POLL = 8;
const POLL = 9;

const SIZES = [
  { width: 360, height: 568 },
  { width: 360, height: 640 },
];

const paused = (slide: number) =>
  presentationState({ slide, step: 0, timer: { state: 'paused', elapsedMs: 760000 } });

// A manual poll, results after close, with a correct answer: the phases with the most controls showing.
const draft = (): PollControlView => {
  const sample = pollControlView('draft', { slide: POLL });
  return {
    ...sample,
    roomSeesCounts: false,
    settings: { ...sample.settings, results: 'afterClose', correct: 2 },
  };
};

test.beforeEach(async ({ page }) => {
  await page.route(`**${view.look.logo!.url}`, (r) =>
    r.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
});

async function openRemote(
  page: Page,
  mount: (component: React.JSX.Element, options: typeof route) => Promise<unknown>,
  slide: number,
) {
  const hub = await stubDeckHub(page, {
    presentation: { state: paused(slide) },
    audience: { state: guestView('live').state },
  });
  await stubApi(page, { '/api/me': { json: me }, [presenterPath]: { json: view } });
  const api = await stubPollControlApi(page, deckId, { polls: [draft()] });
  const firstRead = api.hold('read');
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('speaker-status')).toHaveAttribute('data-status', 'live');
  await expect.poll(() => api.calls.length).toBe(1);
  return { hub, api, firstRead };
}

type Box = { x: number; y: number; width: number; height: number };
type Layout = { next: Box; previous: Box; pageHeight: number; scrollHeight: number };

async function layout(page: Page): Promise<Layout> {
  const next = (await page.getByTestId('speaker-next').boundingBox())!;
  const previous = (await page.getByTestId('speaker-previous').boundingBox())!;
  const pageHeight = (await page.getByTestId('speaker-page').boundingBox())!.height;
  const scrollHeight = await page.evaluate(() => document.documentElement.scrollHeight);
  return { next, previous, pageHeight, scrollHeight };
}

// Next and Previous keep the size they had without the strip and sit wholly inside the viewport.
async function expectNavIntact(page: Page, before: Layout, height: number, state: string) {
  const now = await layout(page);
  for (const [name, box, was] of [
    ['Next', now.next, before.next],
    ['Previous', now.previous, before.previous],
  ] as const) {
    const at = `${name} with the strip ${state}: ${JSON.stringify(box)} (was ${JSON.stringify(was)})`;
    expect(box.width, at).toBeCloseTo(was.width, 0);
    expect(box.height, at).toBeCloseTo(was.height, 0);
    expect(box.y, at).toBeGreaterThanOrEqual(0);
    expect(box.y + box.height, at).toBeLessThanOrEqual(height);
  }
  // The page does not grow taller than the screen, and the row below Next (Slides, Skip deep dives, Blackout) is wholly
  // inside it (SW-278's fix for the 568 px screen).
  expect(now.scrollHeight, `page height with the strip ${state}`).toBe(height);
  for (const id of ['speaker-slides', 'speaker-skip', 'speaker-blackout']) {
    const box = (await page.getByTestId(id).boundingBox())!;
    expect(box.y, `${id} with the strip ${state}`).toBeGreaterThanOrEqual(0);
    expect(box.y + box.height, `${id} with the strip ${state}`).toBeLessThanOrEqual(height);
  }
}

let seq = 9000;

for (const size of SIZES) {
  test(`SpeakerPollStrip_WhenOnAPollSlideAt${size.width}x${size.height}_ShouldKeepNextAndPreviousWholeInEveryPhase`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize(size);
    const { hub, api, firstRead } = await openRemote(page, mount, POLL);
    const before = await layout(page);
    await expect(page.getByTestId(STRIP.strip)).toHaveCount(0);

    firstRead.release();
    const state = page.getByTestId(STRIP.state);
    await expect(state).toHaveText('Draft');
    await expectNavIntact(page, before, size.height, 'in draft');

    // A refused Open: the strip shows its message.
    api.setLive(false);
    // The strip ignores taps for a moment after the phase changes (SW-278), so tap until one is taken.
    await expect(async () => {
      await page.getByTestId(STRIP.open).click();
      await expect(page.getByTestId(STRIP.error)).toBeVisible({ timeout: 400 });
    }).toPass();
    await expectNavIntact(page, before, size.height, 'in draft with an error');

    // Open and hidden from the room: Close and Reveal, with the error still showing from the refusal.
    pushPollControl(hub, {
      ...draft(),
      seq: ++seq,
      phase: 'open',
      round: 'round-9-1',
      answered: 21,
      joined: 41,
    });
    await expect(state).toHaveText('Open');
    await expect(page.getByTestId(STRIP.reveal)).toBeVisible();
    await expectNavIntact(page, before, size.height, 'open');

    // Closed with a correct answer not shown yet: Show answer and New round.
    pushPollControl(hub, {
      ...draft(),
      seq: ++seq,
      phase: 'closed',
      round: 'round-9-1',
      answered: 38,
      joined: 41,
      roomSeesCounts: true,
      rounds: [{ number: 1, answered: 38, joined: 41 }],
    });
    await expect(state).toHaveText('Closed');
    await expect(page.getByTestId(STRIP.showAnswer)).toBeVisible();
    await expectNavIntact(page, before, size.height, 'closed');
  });

  test(`SpeakerPollStrip_WhenOnASlideWithNoPollAt${size.width}x${size.height}_ShouldLeaveThePageAsItWas`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize(size);
    const { api, firstRead } = await openRemote(page, mount, NO_POLL);
    const before = await layout(page);

    firstRead.release();
    // The 404 has landed and been handled: the strip stays away.
    await page.evaluate(() => new Promise((done) => setTimeout(done, 300)));
    expect(api.calls).toEqual([{ call: 'read', slide: NO_POLL }]);
    await expect(page.getByTestId(STRIP.strip)).toHaveCount(0);
    expect(await layout(page)).toEqual(before);
  });
}
