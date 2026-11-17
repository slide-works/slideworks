import { test, expect } from '@playwright/experimental-ct-react';
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { deckPlay } from '../../test-support/fixtures/screens';
import type { LookView } from '../../api/types';
import { lookStyle } from '../../look/lookStyle';
import { JoinProvider } from '../JoinContext';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

const baseLook = lookViewOf('talk', 'cobalt');
const slide = {
  number: 2,
  layout: 'bullets' as const,
  part: null,
  deepDive: false,
  slots: LAYOUT_SLOTS.bullets,
};

function deckWith(joinBar: boolean, look: Partial<LookView> = {}): SlideDeck {
  return {
    title: 'Ship smaller',
    author: 'Sam Rivera',
    parts: [],
    look: { ...baseLook, ...look, joinBar },
    deepDives: [],
  };
}

function mountSlide(
  joinBar: boolean,
  live: boolean,
  look: Partial<LookView> = {},
  template = baseLook,
) {
  return (
    <div style={lookStyle(template)}>
      <JoinProvider deckId={deckPlay.id} join={{ ...deckPlay.join, live }} owner={false}>
        <SlideRenderer slide={slide} deck={deckWith(joinBar, look)} total={4} step={0} />
      </JoinProvider>
    </div>
  );
}

test('the join bar draws a QR code and the code while live with the join bar on', async ({
  mount,
  page,
}) => {
  await mount(mountSlide(true, true));
  const bar = page.getByTestId('join-bar');
  await expect(bar).toBeVisible();
  await expect(page.getByTestId('join-bar-code')).toHaveText(deckPlay.join.codeLabel);
  await expect(page.getByTestId('join-bar-qr').locator('rect')).toHaveAttribute('fill', '#ffffff');
});

test('the join bar stays off when the deck is not live', async ({ mount, page }) => {
  await mount(mountSlide(true, false));
  await expect(page.getByTestId('slide-frame')).toBeVisible();
  await expect(page.getByTestId('join-bar')).toHaveCount(0);
});

test('the join bar stays off when the deck setting is off', async ({ mount, page }) => {
  await mount(mountSlide(false, true));
  await expect(page.getByTestId('slide-frame')).toBeVisible();
  await expect(page.getByTestId('join-bar')).toHaveCount(0);
});

test('the join bar names its QR code by where it goes', async ({ mount, page }) => {
  await mount(mountSlide(true, true));
  await expect(page.getByTestId('join-bar-qr').getByRole('img')).toHaveAccessibleName(
    `QR code to join at ${deckPlay.join.hostLabel} with code ${deckPlay.join.codeLabel}`,
  );
});

test('the join bar is drawn to the mockup: an 88 px QR code in a bar about 116 px tall', async ({
  mount,
  page,
}) => {
  await mount(mountSlide(true, true));
  const qr = (await page.getByTestId('join-bar-qr').boundingBox())!;
  const bar = (await page.getByTestId('join-bar').boundingBox())!;
  expect(qr.width).toBe(88);
  expect(Math.abs(bar.height - 116)).toBeLessThanOrEqual(2);
});

test('the join bar is not drawn on the join slide itself', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={{ ...deckPlay.join, live: true }} owner={false}>
      <SlideRenderer
        slide={{ number: 1, layout: 'join', part: null, deepDive: false, slots: {} }}
        deck={deckWith(true)}
        total={4}
        step={0}
      />
    </JoinProvider>,
  );
  await expect(page.getByTestId('join-qr')).toBeVisible();
  await expect(page.getByTestId('join-bar')).toHaveCount(0);
});

for (const template of ['talk', 'workshop', 'update']) {
  test(`the join bar clears the draft chip, logo and page number under the ${template} template`, async ({
    mount,
    page,
  }) => {
    const look = lookViewOf(template, 'cobalt', {
      draft: true,
      logo: { url: '/api/images/logo-1' },
    });
    await mount(mountSlide(true, true, look, look));
    const bar = (await page.getByTestId('join-bar').boundingBox())!;
    for (const id of ['slide-logo', 'draft-chip', 'slide-footer-text']) {
      const box = (await page.getByTestId(id).boundingBox())!;
      const apart =
        box.x + box.width <= bar.x ||
        bar.x + bar.width <= box.x ||
        box.y + box.height <= bar.y ||
        bar.y + bar.height <= box.y;
      expect(apart, `${id} is clear of the join bar`).toBe(true);
    }
  });
}

test('screenshot: join bar on a content slide', async ({ mount, page }) => {
  await mount(mountSlide(true, true));
  await expect(page.getByTestId('slide-frame')).toHaveScreenshot('join-bar-content.png');
});

test('screenshot: join bar on a dark slide', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={{ ...deckPlay.join, live: true }} owner={false}>
      <SlideRenderer
        slide={{
          number: 1,
          layout: 'close',
          part: null,
          deepDive: false,
          slots: LAYOUT_SLOTS.close,
        }}
        deck={deckWith(true)}
        total={4}
        step={0}
      />
    </JoinProvider>,
  );
  await expect(page.getByTestId('slide-frame')).toHaveScreenshot('join-bar-dark.png');
});

const longHost = 'presentations.example-university.org/j';

test('a 40 character host wraps in the join bar, shows in full and keeps the bar clear', async ({
  mount,
  page,
}) => {
  const look = lookViewOf('talk', 'cobalt', { draft: true, logo: { url: '/api/images/logo-1' } });
  await mount(
    <div style={lookStyle(look)}>
      <JoinProvider
        deckId={deckPlay.id}
        join={{ ...deckPlay.join, live: true, hostLabel: longHost }}
        owner={false}
      >
        <SlideRenderer slide={slide} deck={deckWith(true, look)} total={4} step={0} />
      </JoinProvider>
    </div>,
  );
  const host = page.getByTestId('join-bar').locator('small');
  await expect(host).toHaveText(longHost);
  expect(await host.evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(true);
  const frame = (await page.getByTestId('slide-frame').boundingBox())!;
  const bar = (await page.getByTestId('join-bar').boundingBox())!;
  expect(bar.x).toBeGreaterThanOrEqual(frame.x);
  expect(bar.x + bar.width).toBeLessThanOrEqual(frame.x + 1920);
  expect(bar.y).toBeGreaterThanOrEqual(frame.y);
  expect(bar.y + bar.height).toBeLessThanOrEqual(frame.y + 1080);
  for (const id of ['slide-logo', 'draft-chip', 'slide-footer-text']) {
    const box = (await page.getByTestId(id).boundingBox())!;
    const apart =
      box.x + box.width <= bar.x ||
      bar.x + bar.width <= box.x ||
      box.y + box.height <= bar.y ||
      bar.y + bar.height <= box.y;
    expect(apart, `${id} is clear of the wrapped join bar`).toBe(true);
  }
});
