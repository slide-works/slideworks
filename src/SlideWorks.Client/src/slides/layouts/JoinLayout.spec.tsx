import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import fitFixturesJson from '@contracts/fit-fixtures/join.json' with { type: 'json' };
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { guestView } from '../../test-support/fixtures/guestStates';
import { deckPlay } from '../../test-support/fixtures/screens';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { JoinProvider } from '../JoinContext';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: 'Ship smaller',
  author: 'Sam Rivera',
  parts: [],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};
const join = { ...deckPlay.join, live: true };

function slide(slots: (typeof LAYOUT_SLOTS)['join']) {
  return { number: 1, layout: 'join' as const, part: null, deepDive: false, slots };
}

test('JoinLayout shows the code, the address and the kicker from JoinContext', async ({
  mount,
  page,
}) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('join-code')).toHaveText(join.codeLabel);
  await expect(page.getByTestId('join-host')).toHaveText(join.hostLabel);
  await expect(page.getByTestId('join-kicker')).toHaveText('Join on your phone');
  await expect(page.getByTestId('slide-frame')).toHaveAttribute('data-dark', 'true');
});

test('JoinLayout shows the count only while live and not hidden', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={{ ...join, live: false }} owner={false}>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('layout-join')).toBeVisible();
  await expect(page.getByTestId('join-count')).toHaveCount(0);
  await expect(page.getByTestId('join-not-live')).toContainText('Not live yet');
});

test('JoinLayout draws the QR code 780 px wide, black on white', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  const qr = page.getByTestId('join-qr');
  const box = await qr.boundingBox();
  expect(box?.width).toBe(780);
  expect(box?.height).toBe(780);
  await expect(qr.locator('rect')).toHaveAttribute('fill', '#ffffff');
  await expect(qr.locator('path')).toHaveAttribute('fill', '#000000');
});

test('JoinLayout leaves the count off when the slot hides it', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <SlideRenderer slide={slide({ count: 'hidden' })} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('join-code')).toBeVisible();
  await expect(page.getByTestId('join-count')).toHaveCount(0);
});

test('JoinLayout with the longest kicker stays inside x 120-1800, y 150-996', async ({
  mount,
  page,
}) => {
  await stubOwnerLive(page, 5);
  await mount(
    <JoinProvider deckId={deckPlay.id} join={{ ...join, live: true }} owner>
      <SlideRenderer
        slide={slide({ kicker: fitFixturesJson.joinKicker.slots.kicker, count: 'shown' })}
        deck={deck}
        total={1}
        step={0}
      />
    </JoinProvider>,
  );
  const frame = (await page.getByTestId('slide-frame').boundingBox())!;
  for (const id of ['join-qr', 'join-kicker', 'join-code', 'join-host', 'join-count']) {
    const box = (await page.getByTestId(id).boundingBox())!;
    expect(box.x - frame.x, `${id} left`).toBeGreaterThanOrEqual(119.5);
    expect(box.x + box.width - frame.x, `${id} right`).toBeLessThanOrEqual(1800.5);
    expect(box.y - frame.y, `${id} top`).toBeGreaterThanOrEqual(149.5);
    expect(box.y + box.height - frame.y, `${id} bottom`).toBeLessThanOrEqual(996.5);
  }
  const code =
    (await page.getByTestId('join-code').evaluate((el) => el.scrollWidth)) <=
    (await page.getByTestId('join-code').evaluate((el) => el.clientWidth));
  expect(code).toBe(true);
});

test('JoinLayout names the QR code by where it goes', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('join-qr').getByRole('img')).toHaveAccessibleName(
    `QR code to join at ${join.hostLabel} with code ${join.codeLabel}`,
  );
});

test('JoinLayout draws no join bar even when the deck setting is on', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <SlideRenderer
        slide={slide({})}
        deck={{ ...deck, look: { ...deck.look, joinBar: true } }}
        total={1}
        step={0}
      />
    </JoinProvider>,
  );
  await expect(page.getByTestId('join-qr')).toBeVisible();
  await expect(page.getByTestId('join-bar')).toHaveCount(0);
});

for (const live of [true, false]) {
  test(`screenshot: join slide ${live ? 'live' : 'not live'}`, async ({ mount, page }) => {
    // Mounted as the owner, so the live slide shows the joined count, as the mockup does.
    if (live) await stubOwnerLive(page, 8);
    await mount(
      <JoinProvider deckId={deckPlay.id} join={{ ...join, live }} owner={live}>
        <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
      </JoinProvider>,
    );
    await expect(page.getByTestId('slide-frame')).toHaveScreenshot(
      `join-slide-${live ? 'live' : 'not-live'}.png`,
    );
  });
}

test('JoinLayout shows a 40 character host in full inside x 120-1800', async ({ mount, page }) => {
  const hostLabel = 'presentations.example-university.org/j';
  await stubOwnerLive(page, 5);
  await mount(
    <JoinProvider deckId={deckPlay.id} join={{ ...join, hostLabel }} owner>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  const host = page.getByTestId('join-host');
  await expect(host).toHaveText(hostLabel);
  expect(await host.evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(true);
  const frame = (await page.getByTestId('slide-frame').boundingBox())!;
  for (const id of ['join-host', 'join-count']) {
    const box = (await page.getByTestId(id).boundingBox())!;
    expect(box.x + box.width - frame.x, `${id} right`).toBeLessThanOrEqual(1800.5);
    expect(box.y + box.height - frame.y, `${id} bottom`).toBeLessThanOrEqual(996.5);
  }
});

// The owner's live session, which is the only place the count is known (a non-owner's is null and hidden).
async function stubOwnerLive(page: Page, joinedCount: number) {
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await page.route(`**/api/decks/${deckPlay.id}/live`, (route) =>
    route.fulfill({
      json: {
        sessionId: 'session-1',
        code: join.code,
        codeLabel: join.codeLabel,
        joinUrl: join.joinUrl,
        hostLabel: join.hostLabel,
        live: true,
        joinedCount,
        joinBar: false,
      },
    }),
  );
}

test('JoinLayout hides the count when it is not known', async ({ mount, page }) => {
  // A non-owner on a live deck: the count is null, so there is nothing to show.
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('layout-join')).toBeVisible();
  await expect(page.getByTestId('join-count')).toHaveCount(0);
});

test('JoinLayout shows a known count of 0 as "0 joined"', async ({ mount, page }) => {
  await stubOwnerLive(page, 0);
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('join-count')).toHaveText('0 joined');
});
