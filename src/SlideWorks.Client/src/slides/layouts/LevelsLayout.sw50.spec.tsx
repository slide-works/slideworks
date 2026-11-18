import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { LevelsSlots } from '../slots/levels';
import type { SlideDeck } from '../slots';

// The levels layout: the ladder on either side, the tiles at the foot of the
// main column, an image that fails to load, and the fit fixtures (with wide
// twins), which also show that the ladder and the tiles clip their overflow.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function levelsSlide(slots: LevelsSlots, number = 14): RenderableSlide {
  return { number, layout: 'levels', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const base: LevelsSlots = {
  kicker: 'The cost',
  headline: 'Every layer rests on the one under it',
  levels: {
    title: 'Maturity',
    items: [
      { text: 'Continuous flow', mark: '3' },
      { text: 'Small batches', mark: '2', state: 'current' },
      { text: 'Big bang', mark: '1' },
    ],
  },
  main: { title: 'Where we are', items: ['We ship weekly', 'We review in pairs'] },
  facts: [
    { label: 'Cycle time', value: '9 days' },
    { label: 'Slips', value: '2 a year' },
  ],
};

const box = (page: Page, testId: string) => page.getByTestId(testId).boundingBox();

test('side right is the default: the ladder is at the right edge', async ({ mount, page }) => {
  await mount(render(levelsSlide(base)));
  const main = (await box(page, 'levels-main'))!;
  const ladder = (await box(page, 'block-levels'))!;
  expect(main.x).toBeCloseTo(120, 0);
  expect(main.width).toBeCloseTo(1144, 0);
  expect(main.x + main.width).toBeLessThanOrEqual(ladder.x);
  expect(ladder.x + ladder.width).toBeCloseTo(1800, 0);
  expect(ladder.width).toBeCloseTo(480, 0);
});

test('side left puts the ladder on the left', async ({ mount, page }) => {
  await mount(render(levelsSlide({ ...base, side: 'left' })));
  const main = (await box(page, 'levels-main'))!;
  const ladder = (await box(page, 'block-levels'))!;
  expect(ladder.x).toBeCloseTo(120, 0);
  expect(ladder.x + ladder.width).toBeLessThanOrEqual(main.x);
  expect(main.x - (ladder.x + ladder.width)).toBeCloseTo(56, 0);
  expect(main.x + main.width).toBeCloseTo(1800, 0);
});

test('tiles sit at the bottom of the main column', async ({ mount, page }) => {
  await mount(render(levelsSlide(base)));
  const tiles = (await box(page, 'block-facts'))!;
  const main = (await box(page, 'levels-main'))!;
  // A short column still ends on the tiles, not at the foot of its points.
  expect(tiles.y + tiles.height).toBeCloseTo(main.y + main.height, 0);
  const lastPoint = (await page.getByTestId('two-column-item').last().boundingBox())!;
  expect(tiles.y).toBeGreaterThan(lastPoint.y + lastPoint.height);
});

test('a callout sits under the tiles at the foot of the column', async ({ mount, page }) => {
  const callout = { tone: 'accent', text: 'Cut the batch.', reveal: 'last' } as const;
  const component = await mount(render(levelsSlide({ ...base, callout }), 0));
  const hidden = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(hidden).toHaveAttribute('data-revealed', 'false');
  await component.update(render(levelsSlide({ ...base, callout }), 1));
  await expect(hidden).toHaveAttribute('data-revealed', 'true');
  const tiles = (await box(page, 'block-facts'))!;
  const drawn = (await box(page, 'body-callout'))!;
  const main = (await box(page, 'levels-main'))!;
  expect(drawn.y).toBeGreaterThanOrEqual(tiles.y + tiles.height);
  expect(drawn.y + drawn.height).toBeCloseTo(main.y + main.height, 0);
});

test('an image in main fills what is left above the tiles', async ({ mount, page }) => {
  await page.route('**/api/images/*', (route) =>
    route.fulfill({
      contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="800" height="400"><rect width="800" height="400" fill="gray"/></svg>',
    }),
  );
  await mount(
    render(levelsSlide({ ...base, main: { image: { image: 'img_pic', alt: 'A picture' } } })),
  );
  const image = page.getByTestId('two-column-image');
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.naturalWidth))
    .toBeGreaterThan(0);
  const picture = (await image.boundingBox())!;
  const tiles = (await box(page, 'block-facts'))!;
  expect(picture.y + picture.height).toBeLessThanOrEqual(tiles.y);
});

test('an image in main keeps its alt text when it fails to load', async ({ mount, page }) => {
  await page.route('**/api/images/*', (route) => route.abort());
  await mount(
    render(
      levelsSlide({
        ...base,
        main: { image: { image: 'img_pic', alt: 'A ladder of five levels' } },
      }),
    ),
  );
  const image = page.getByTestId('two-column-image');
  await expect(image).toHaveAttribute('alt', 'A ladder of five levels');
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
    .toBe(0);
  await expect(page.getByRole('img', { name: 'A ladder of five levels' })).toHaveCount(1);
  await expect(page.getByTestId('levels-rung')).toHaveCount(3);
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      levelsSlide({
        ...base,
        headline: 'Small steps beat big plans',
        emphasis: [{ text: 'big plans', tone: 'negative' }],
      }),
    ),
  );
  await expect(page.getByTestId('emphasis')).toHaveAttribute('data-tone', 'negative');
});

// Wide letters (capitals, W and M) of exactly `length` characters. Copied from
// DataLayouts.fixtures.ts, which is frozen.
const WIDE_TEXT = 'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE_TEXT;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

// Keys that are not text and stay as they are, whatever they hold.
const NOT_TEXT = new Set(['reveal', 'tone', 'fill', 'in', 'side', 'state', 'icon', 'image', 'alt']);
function widen(value: unknown, key?: string): unknown {
  if (key !== undefined && NOT_TEXT.has(key)) return value;
  if (typeof value === 'string') return wide(value.length);
  if (Array.isArray(value)) return value.map((item) => widen(item));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, widen(v, k)]));
  }
  return value;
}

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-levels').evaluate((root: Element) => {
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
        // Only what is drawn: a box that clips its overflow cuts the text at its edge.
        let { left: l, right: r, bottom: b, top: t } = rect;
        for (let el = node.parentElement; el !== null && el !== root; el = el.parentElement) {
          if (getComputedStyle(el).overflow === 'visible') continue;
          const clip = el.getBoundingClientRect();
          l = Math.max(l, clip.left);
          r = Math.min(r, clip.right);
          b = Math.min(b, clip.bottom);
          t = Math.max(t, clip.top);
        }
        if (r <= l || b <= t) continue;
        left = Math.min(left, l - slide.left);
        right = Math.max(right, r - slide.left);
        bottom = Math.max(bottom, b - slide.top);
      }
    }
    return { left, right, bottom };
  });
}

// Bottom edge, in slide pixels, of the element with a test id.
function boxBottom(page: Page, testId: string) {
  return page.getByTestId(testId).evaluate((el: Element) => {
    const slide = el.closest('[data-layout]')!.getBoundingClientRect();
    return el.getBoundingClientRect().bottom - slide.top;
  });
}

async function bottoms(page: Page) {
  const tiles = (await page.getByTestId('block-facts').count()) > 0;
  return {
    ladder: await boxBottom(page, 'block-levels'),
    main: await boxBottom(page, 'levels-main'),
    tiles: tiles ? await boxBottom(page, 'block-facts') : 0,
  };
}

for (const name of ['levelsSeven', 'levelsText', 'levelsTextWide']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = fitFixture(name).slots as LevelsSlots;
    // Every step shown, so a callout that reveals last is drawn.
    await mount(render(levelsSlide(slots), 20));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    // Half a pixel of slack: the body starts at y 376.2 under a two-line headline, and the
    // ladder's 620 px clip ends at 996.2.
    expect(drawn.bottom).toBeLessThanOrEqual(996.5);
    const b = await bottoms(page);
    expect(b.ladder).toBeLessThanOrEqual(996.5);
    expect(b.main).toBeLessThanOrEqual(996);
    // Nothing is cut off in the ladder or the main column at the limits.
    const cut = await page
      .getByTestId('levels-rung')
      .evaluateAll((rungs) => rungs.filter((r) => r.scrollWidth > r.clientWidth + 1).length);
    expect(cut).toBe(0);
    test.info().annotations.push({ type: 'bottoms', description: JSON.stringify(b) });
  });
}

for (const name of ['levelsSeven', 'levelsText']) {
  test(`${name} wide twin stays between x 120 and 1800`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = widen(fitFixture(name).slots) as LevelsSlots;
    await mount(render(levelsSlide(slots, 20)));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    // The ladder card and the tile row clip what the wide letters push past them (D1),
    // and the card itself stays on the slide.
    await expect(page.getByTestId('block-levels')).toHaveCSS('overflow', 'hidden');
    if ((await page.getByTestId('block-facts').count()) > 0) {
      await expect(page.getByTestId('block-facts')).toHaveCSS('overflow', 'hidden');
    }
    const b = await bottoms(page);
    expect(b.ladder).toBeLessThanOrEqual(996.5);
    test
      .info()
      .annotations.push({ type: 'bottom', description: `${drawn.bottom} ${JSON.stringify(b)}` });
  });
}

test('screenshot: levels seven', async ({ mount, page }) => {
  await stubApi(page, {});
  const slots = fitFixture('levelsSeven').slots as LevelsSlots;
  const component = await mount(render(levelsSlide(slots)));
  await expect(component).toHaveScreenshot('layout-levels-seven.png');
});
