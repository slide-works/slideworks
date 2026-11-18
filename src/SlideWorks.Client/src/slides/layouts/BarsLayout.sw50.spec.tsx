import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { planBars, barHeight, TRACK } from '../diagrams/barsGeometry';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { BarsSlots } from '../slots/bars';
import type { SlideDeck } from '../slots';

// The bars layout: where a note goes, bar lengths that follow barsGeometry,
// hidden numbers, the dashed outline and its legend, steps that keep their
// space, the figure's description, the clipped figure and the two fit fixtures.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function barsSlide(slots: BarsSlots, number = 12): RenderableSlide {
  return { number, layout: 'bars', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const base: BarsSlots = {
  kicker: 'The cost',
  headline: 'Every slip started as a big batch',
  bars: [
    { label: 'Release', value: 90 },
    { label: 'Review', value: 60 },
    { label: 'Build', value: 40 },
  ],
};

const rows = (page: Page) => page.getByTestId('bars-bar');
const box = (page: Page, testId: string) => page.getByTestId(testId).first().boundingBox();

test("a short bar's note sits after it", async ({ mount, page }) => {
  await mount(
    render(
      barsSlide({
        ...base,
        bars: [
          { label: 'Long', value: 100, note: 'Inside the long bar' },
          { label: 'Short', value: 10, note: 'After the short one' },
        ],
      }),
    ),
  );
  const fills = await page.getByTestId('bars-fill').evaluateAll((els) =>
    els.map((el) => {
      const r = el.getBoundingClientRect();
      return { left: r.left, right: r.right };
    }),
  );
  const notes = page.getByTestId('bars-note');
  const inside = (await notes.nth(0).boundingBox())!;
  const after = (await notes.nth(1).boundingBox())!;
  expect(inside.x + inside.width).toBeLessThanOrEqual(fills[0].right + 1);
  expect(after.x).toBeGreaterThanOrEqual(fills[1].right + 17);
  // The value follows the note.
  const value = (await page.getByTestId('bars-value').nth(1).boundingBox())!;
  expect(value.x).toBeGreaterThanOrEqual(after.x + after.width);
});

test('bar width matches barsGeometry', async ({ mount, page }) => {
  const slots: BarsSlots = {
    ...base,
    unit: 'wks',
    bars: [
      { label: 'Release', value: 90, tag: 'Late' },
      { label: 'Review', value: 60, was: 120 },
      { label: 'Build', value: 0 },
    ],
  };
  await mount(render(barsSlide(slots)));
  const plan = planBars(slots.bars, slots.unit, true);
  const widths = await page
    .getByTestId('bars-fill')
    .evaluateAll((els) => els.map((el) => el.getBoundingClientRect().width));
  widths.forEach((w, i) => expect(Math.abs(w - plan.placements[i].length)).toBeLessThanOrEqual(1));
  const outline = (await box(page, 'bars-was'))!;
  expect(Math.abs(outline.width - plan.placements[1].outline!)).toBeLessThanOrEqual(1);
  expect(widths[2]).toBeCloseTo(6, 0);
  const track = await page
    .getByTestId('bars-fill')
    .first()
    .evaluate((el) => el.parentElement!.getBoundingClientRect().width);
  expect(track).toBeCloseTo(TRACK, 0);
  const heights = await rows(page).evaluateAll((els) =>
    els.map((el) => Math.round(el.getBoundingClientRect().height)),
  );
  expect(heights).toEqual([barHeight(3), barHeight(3), barHeight(3)]);
});

test('values hidden draws no numbers', async ({ mount, page }) => {
  const component = await mount(render(barsSlide({ ...base, unit: 'wks' })));
  await expect(page.getByTestId('bars-value').first()).toHaveText('90 wks');
  await component.update(render(barsSlide({ ...base, unit: 'wks', values: 'hidden' })));
  await expect(page.getByTestId('bars-value')).toHaveCount(0);
  await expect(page.getByTestId('bars-figure')).not.toContainText('wks');
});

test('the was outline is dashed and the legend names it', async ({ mount, page }) => {
  await mount(
    render(
      barsSlide({
        ...base,
        wasLabel: 'Last year',
        bars: [
          { label: 'Release', value: 60, was: 90 },
          { label: 'Review', value: 60 },
        ],
      }),
    ),
  );
  await expect(page.getByTestId('bars-was')).toHaveCount(1);
  await expect(page.getByTestId('bars-was')).toHaveCSS('border-top-style', 'dashed');
  await expect(page.getByTestId('bars-was')).toHaveCSS('border-top-width', '3px');
  await expect(page.getByTestId('bars-legend')).toHaveText('Last year');
  const legend = (await box(page, 'bars-legend'))!;
  const figure = (await box(page, 'bars-figure'))!;
  expect(legend.height).toBeCloseTo(38, 0);
  expect(legend.x + legend.width).toBeCloseTo(figure.x + figure.width, 0);
  expect((await box(page, 'bars-bar'))!.y).toBeGreaterThanOrEqual(legend.y + 38 - 1);
});

test('hidden bars keep their space', async ({ mount, page }) => {
  const slots: BarsSlots = { ...base, reveal: 'one-by-one' };
  const component = await mount(render(barsSlide(slots), 1));
  const reveals = rows(page).locator('.sw-reveal');
  await expect(reveals.nth(0)).toHaveAttribute('data-revealed', 'true');
  await expect(reveals.nth(1)).toHaveAttribute('data-revealed', 'false');
  await expect(reveals.nth(2)).toHaveAttribute('data-revealed', 'false');
  const before = await rows(page).evaluateAll((els) =>
    els.map((el) => {
      const r = el.getBoundingClientRect();
      return [r.top, r.height];
    }),
  );
  expect(before.every(([, h]) => h >= 96)).toBe(true);
  await component.update(render(barsSlide(slots), 3));
  await expect(reveals.nth(2)).toHaveAttribute('data-revealed', 'true');
  const after = await rows(page).evaluateAll((els) =>
    els.map((el) => {
      const r = el.getBoundingClientRect();
      return [r.top, r.height];
    }),
  );
  expect(after).toEqual(before);
});

test('the figure has one sentence for screen readers', async ({ mount, page }) => {
  await mount(
    render(
      barsSlide({
        ...base,
        unit: 'ms',
        bars: [
          { label: 'Edge', value: 10, tag: 'Fast' },
          { label: 'Core', value: 30, note: 'Slow' },
        ],
      }),
    ),
  );
  await expect(page.getByTestId('bars-figure')).toHaveAttribute(
    'aria-label',
    'Bars comparing 2 things: Edge 10 ms (Fast); Core 30 ms, Slow.',
  );
});

test('the figure box clips its overflow', async ({ mount, page }) => {
  await mount(render(barsSlide(base)));
  await expect(page.getByTestId('bars-figure')).toHaveCSS('overflow', 'hidden');
});

test('a callout that reveals last arrives after the bars', async ({ mount, page }) => {
  const slots: BarsSlots = {
    ...base,
    reveal: 'one-by-one',
    callout: { tone: 'accent', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(barsSlide(slots), 3));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(barsSlide(slots), 4));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      barsSlide({
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

// Every text string widened. Options (tone, values, reveal, in) stay as they
// are, and so do the numbers (value, was).
const NOT_TEXT = new Set(['reveal', 'tone', 'values', 'fill', 'in']);
function widen(value: unknown, key?: string): unknown {
  if (typeof value === 'string')
    return key !== undefined && NOT_TEXT.has(key) ? value : wide(value.length);
  if (Array.isArray(value)) return value.map((item) => widen(item));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, widen(v, k)]));
  }
  return value;
}

// The left and right extents, and the bottom, of the text runs in the layout
// that show: a run in the figure is cut to the figure box, which clips what is
// past it, and one wholly past it is not counted. `clipped` is the lowest text
// bottom with no cut, which is where a too-wide font would have drawn.
function textBox(page: Page) {
  return page.getByTestId('layout-bars').evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const figure = root.querySelector('[data-testid="bars-figure"]')!.getBoundingClientRect();
    let left = Infinity;
    let right = -Infinity;
    let bottom = -Infinity;
    let uncut = -Infinity;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const range = document.createRange();
      range.selectNodeContents(node);
      const inFigure = node.parentElement!.closest('figure') !== null;
      for (const rect of range.getClientRects()) {
        if (rect.width === 0) continue;
        uncut = Math.max(uncut, rect.bottom - slide.top);
        const l = inFigure ? Math.max(rect.left, figure.left) : rect.left;
        const r = inFigure ? Math.min(rect.right, figure.right) : rect.right;
        const b = inFigure ? Math.min(rect.bottom, figure.bottom) : rect.bottom;
        if (r <= l || (inFigure && rect.top >= figure.bottom)) continue;
        left = Math.min(left, l - slide.left);
        right = Math.max(right, r - slide.left);
        bottom = Math.max(bottom, b - slide.top);
      }
    }
    return { left, right, bottom, uncut };
  });
}

// Bottom edge, in slide pixels, of the element with a test id.
function boxBottom(page: Page, testId: string) {
  return page.getByTestId(testId).evaluate((el: Element) => {
    const slide = el.closest('[data-layout]')!.getBoundingClientRect();
    return el.getBoundingClientRect().bottom - slide.top;
  });
}

// The rows' own heights, the bottom of the figure, and how far any text run
// reaches past the figure on the right (clipped, so only a measure).
async function heights(page: Page) {
  const figure = await boxBottom(page, 'bars-figure');
  const height = (await box(page, 'bars-figure'))!.height;
  const rowHeights = await rows(page).evaluateAll((els) =>
    els.map((el) => Math.round(el.getBoundingClientRect().height)),
  );
  return { figure, height: Math.round(height), rowHeights };
}

for (const name of ['barsSix', 'barsNote']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    const fixture = fitFixture(name);
    await mount(render(barsSlide(fixture.slots as BarsSlots), 20));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    const h = await heights(page);
    expect(h.figure).toBeLessThanOrEqual(996);
    // The drawn figure is the server's estimate, to a pixel a row.
    expect(Math.abs(h.height - (fixture.estimate ?? 0))).toBeLessThanOrEqual(8);
    // No text runs past its own bar's track.
    const overflow = await page
      .getByTestId('bars-figure')
      .evaluate((figure) => figure.scrollWidth - figure.clientWidth);
    expect(overflow).toBe(0);
    test.info().annotations.push({ type: 'heights', description: JSON.stringify(h) });
  });

  test(`${name} wide twin stays between x 120 and 1800`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = widen(fitFixture(name).slots) as BarsSlots;
    // The numbers stay: the lengths still follow the values.
    const original = fitFixture(name).slots as BarsSlots;
    slots.bars.forEach((bar, i) => {
      bar.value = original.bars[i].value;
      if (original.bars[i].was !== undefined) bar.was = original.bars[i].was;
    });
    await mount(render(barsSlide(slots), 20));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    // What shows ends inside the box: the figure clips the rest, so a wide font
    // cannot draw over the callout or past the slide. The estimate is not met:
    // wide letters wrap sooner than it counts, and `uncut` is how far down they
    // would have drawn.
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    const h = await heights(page);
    test.info().annotations.push({
      type: 'bottom',
      description: `${drawn.bottom} uncut ${drawn.uncut} ${JSON.stringify(h)}`,
    });
  });
}

test('screenshot: bars six', async ({ mount, page }) => {
  await stubApi(page, {});
  const component = await mount(render(barsSlide(fitFixture('barsSix').slots as BarsSlots), 20));
  await expect(component).toHaveScreenshot('layout-bars-six.png');
});

test('screenshot: bars with was outline', async ({ mount }) => {
  const component = await mount(
    render(
      barsSlide({
        ...base,
        unit: 'wks',
        wasLabel: 'Last year',
        caption: 'Weeks from idea to customer',
        bars: [
          { label: 'Release', value: 9, was: 12, note: 'One big drop', tone: 'negative' },
          { label: 'Review', value: 6, was: 6 },
          { label: 'Build', value: 4, was: 5, tone: 'positive', tag: 'Better' },
        ],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-bars-with-was-outline.png');
});
