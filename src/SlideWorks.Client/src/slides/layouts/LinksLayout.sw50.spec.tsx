import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { qrMatrix } from '../../qr/qrMatrix';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { LinksSlots } from '../slots/links';
import type { SlideDeck } from '../slots';

// The links layout: 1–6 links that open in a new tab, an optional QR code, and
// no steps.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

// In a real part, to prove a deck with parts still draws a tracker as before.
function links(slots: LinksSlots): RenderableSlide {
  return { number: 3, layout: 'links', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, slideDeck: SlideDeck = deck) {
  return <SlideRenderer slide={slide} deck={slideDeck} total={sampleDeck.slides.length} step={0} />;
}

const base: LinksSlots = {
  kicker: 'Read more',
  headline: 'Where to read more about small batches',
  links: [
    {
      kind: 'Talk',
      title: 'Small batches, big wins',
      source: 'Conference keynote',
      url: 'https://example.com/talks/small-batches',
    },
    { kind: 'Article', title: 'The cost of a big batch', url: 'https://example.com/a?b=1&c=2' },
  ],
};

const withQr: LinksSlots = {
  ...base,
  qr: { url: 'https://example.com/small-batches', caption: 'example.com/small-batches' },
};

test('anchors carry href, target and rel', async ({ mount, page }) => {
  await mount(render(links(base)));
  const anchors = page.getByTestId('links-list').locator('a');
  await expect(anchors).toHaveCount(2);
  await expect(anchors.nth(0)).toHaveAttribute('href', 'https://example.com/talks/small-batches');
  await expect(anchors.nth(0)).toHaveAttribute('target', '_blank');
  await expect(anchors.nth(0)).toHaveAttribute('rel', 'noopener noreferrer');
  await expect(anchors.nth(1)).toHaveAttribute('href', 'https://example.com/a?b=1&c=2');
  await expect(anchors.nth(0)).toHaveText('Small batches, big wins');
});

test('an unsafe address draws plain text and no anchor', async ({ mount, page }) => {
  const unsafe = 'javascript:alert(1)';
  await mount(
    render(
      links({
        ...base,
        links: [{ title: 'Click <b>me</b>', url: unsafe }, ...base.links.slice(0, 1)],
      }),
    ),
  );
  const rows = page.getByTestId('links-row');
  await expect(rows.nth(0).locator('a')).toHaveCount(0);
  await expect(rows.nth(0).getByTestId('links-title')).toHaveText('Click <b>me</b>');
  await expect(rows.nth(0).locator('b')).toHaveCount(0);
  await expect(page.locator(`[href="${unsafe}"]`)).toHaveCount(0);
  await expect(rows.nth(1).locator('a')).toHaveCount(1);
});

test('QR encodes the address and carries its label', async ({ mount, page }) => {
  await mount(render(links(withQr)));
  const svg = page.getByTestId('qr-code');
  await expect(svg).toHaveAttribute('aria-label', 'QR code for example.com/small-batches');
  const matrix = qrMatrix('https://example.com/small-batches');
  const dark = matrix.flatMap((row, y) =>
    row.flatMap((on, x) => (on ? [`M${x + 4} ${y + 4}h1v1h-1z`] : [])),
  );
  expect(await svg.locator('path').getAttribute('d')).toBe(dark.join(''));
  const box = (await page.getByTestId('links-qr').locator('svg').boundingBox())!;
  expect(Math.round(box.width)).toBe(400);
  expect(Math.round(box.height)).toBe(400);
});

test('an unsafe QR address draws no code', async ({ mount, page }) => {
  await mount(render(links({ ...base, qr: { url: 'javascript:alert(1)' } })));
  await expect(page.getByTestId('qr-code')).toHaveCount(0);
});

test('a clamped caption ends with an ellipsis', async ({ mount, page }) => {
  await mount(
    render(links({ ...base, qr: { url: 'https://example.com/a', caption: 'x'.repeat(200) } })),
  );
  await expect(page.getByTestId('links-qr-caption')).toHaveCSS('text-overflow', 'ellipsis');
});

test('caption defaults to the address without its scheme', async ({ mount, page }) => {
  await mount(render(links({ ...base, qr: { url: 'https://example.com/small-batches' } })));
  await expect(page.getByTestId('links-qr-caption')).toHaveText('example.com/small-batches');
  await expect(page.getByTestId('qr-code')).toHaveAttribute(
    'aria-label',
    'QR code for example.com/small-batches',
  );
});

test('a deck that is not interactive draws anchors with tabindex -1', async ({ mount, page }) => {
  await mount(render(links(withQr), { ...deck, interactive: false }));
  const anchors = page.getByTestId('links-list').locator('a');
  await expect(anchors).toHaveCount(2);
  for (const index of [0, 1]) {
    await expect(anchors.nth(index)).toHaveAttribute('tabindex', '-1');
    expect(await anchors.nth(index).evaluate((a) => getComputedStyle(a).pointerEvents)).toBe(
      'none',
    );
  }
});

test('an interactive deck leaves anchors focusable and clickable', async ({ mount, page }) => {
  await mount(render(links(base)));
  const anchor = page.getByTestId('links-list').locator('a').first();
  await expect(anchor).not.toHaveAttribute('tabindex', /.*/);
  expect(await anchor.evaluate((a) => getComputedStyle(a).pointerEvents)).not.toBe('none');
});

test('links draws a tracker in a real part, no steps and no callout', async ({ mount, page }) => {
  await mount(render(links(withQr)));
  await expect(page.getByTestId('layout-links')).toBeVisible();
  await expect(page.locator('.sw-reveal')).toHaveCount(0);
});

const WIDE_TEXT =
  'Many Women Who Wander Wide Meadows Mow Moss With Me When Warm, ' +
  'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';

// Wide-letter text (capitals, W and M) of exactly `length` characters.
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE_TEXT;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

// Slot keys whose strings are addresses: widening one would make it invalid.
const NOT_TEXT = new Set(['url']);

function widen(value: unknown, key?: string): unknown {
  if (typeof value === 'string')
    return key !== undefined && NOT_TEXT.has(key) ? value : wide(value.length);
  if (Array.isArray(value)) return value.map((item) => widen(item));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, widen(v, k)]));
  }
  return value;
}

async function drawn(page: Page) {
  return page.getByTestId('layout-links').evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    let left = Infinity;
    let right = -Infinity;
    let bottom = -Infinity;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) {
        if (rect.width === 0) continue;
        left = Math.min(left, rect.left - slide.left);
        right = Math.max(right, rect.right - slide.left);
        bottom = Math.max(bottom, rect.bottom - slide.top);
      }
    }
    for (const row of root.querySelectorAll(
      '[data-testid="links-row"], [data-testid="links-qr"]',
    )) {
      const rect = row.getBoundingClientRect();
      left = Math.min(left, rect.left - slide.left);
      right = Math.max(right, rect.right - slide.left);
      bottom = Math.max(bottom, rect.bottom - slide.top);
    }
    return { left, right, bottom };
  });
}

for (const name of ['linksSixQr', 'linksWrapped']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    await mount(render(links(fitFixture(name).slots as LinksSlots)));
    await page.evaluate(() => document.fonts.ready);
    const box = await drawn(page);
    expect(box.left).toBeGreaterThanOrEqual(120);
    expect(box.right).toBeLessThanOrEqual(1800);
    expect(box.bottom).toBeLessThanOrEqual(996);
  });

  // Drawn in wide letters of the same lengths (the fixture is refused by fit,
  // so there is no twin in the JSON): the sides must hold. Wide letters wrap
  // more than the estimate counts, so the bottom is measured and reported, not
  // asserted.
  test(`${name} in wide letters stays between the sides`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = widen(fitFixture(name).slots) as LinksSlots;
    await mount(render(links(slots)));
    await page.evaluate(() => document.fonts.ready);
    const box = await drawn(page);
    expect(box.left).toBeGreaterThanOrEqual(120);
    expect(box.right).toBeLessThanOrEqual(1800);
    test.info().annotations.push({ type: 'bottom', description: String(box.bottom) });
  });
}

test('screenshot: links with QR', async ({ mount }) => {
  const component = await mount(render(links(withQr)));
  await expect(component).toHaveScreenshot('layout-links-qr.png');
});

test('screenshot: links without QR', async ({ mount }) => {
  const component = await mount(render(links(base)));
  await expect(component).toHaveScreenshot('layout-links-plain.png');
});
