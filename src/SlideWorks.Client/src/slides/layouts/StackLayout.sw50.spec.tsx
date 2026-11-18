import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { StackSlots } from '../slots/stack';
import type { SlideDeck } from '../slots';

// The stack layout: segments revealed from the bottom up, heights that follow
// the values, the side the stack is on, the centred figure, its description
// and the two fit fixtures.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function stackSlide(slots: StackSlots, number = 12): RenderableSlide {
  return { number, layout: 'stack', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const base: StackSlots = {
  kicker: 'The cost',
  headline: 'Every layer rests on the one under it',
  segments: [{ label: 'Customers' }, { label: 'Services' }, { label: 'Storage' }],
  points: ['Fix the lowest layer first', { lead: 'Cheap', text: 'Changes are small and local.' }],
};

const segments = (page: Page) => page.getByTestId('stack-segment');
const box = (page: Page, testId: string) => page.getByTestId(testId).boundingBox();

test('reveals from the bottom up', async ({ mount, page }) => {
  const slots: StackSlots = { ...base, reveal: 'one-by-one' };
  const component = await mount(render(stackSlide(slots), 1));
  const reveals = page.getByTestId('stack-segment').locator('.sw-reveal');
  await expect(reveals.nth(0)).toHaveAttribute('data-revealed', 'false');
  await expect(reveals.nth(1)).toHaveAttribute('data-revealed', 'false');
  await expect(reveals.nth(2)).toHaveAttribute('data-revealed', 'true');
  // The hidden segments keep their space.
  const before = await segments(page).evaluateAll((els) =>
    els.map((el) => el.getBoundingClientRect().height),
  );
  expect(before.every((h) => h >= 44)).toBe(true);
  await component.update(render(stackSlide(slots), 3));
  await expect(reveals.nth(0)).toHaveAttribute('data-revealed', 'true');
  const after = await segments(page).evaluateAll((els) =>
    els.map((el) => el.getBoundingClientRect().height),
  );
  expect(after).toEqual(before);
});

test('twice the value is about twice as tall', async ({ mount, page }) => {
  await mount(
    render(
      stackSlide({
        ...base,
        segments: [
          { label: 'Big', value: 6 },
          { label: 'Small', value: 3 },
          { label: 'Tiny', value: 1 },
        ],
      }),
    ),
  );
  const heights = await segments(page).evaluateAll((els) =>
    els.map((el) => el.getBoundingClientRect().height),
  );
  expect(Math.abs(heights[0] / heights[1] - 2)).toBeLessThan(0.2);
});

test('a value and its unit are drawn in the segment', async ({ mount, page }) => {
  await mount(
    render(
      stackSlide({
        ...base,
        unit: 'wks',
        segments: [
          { label: 'Build', value: 2.5, tag: 'New' },
          { label: 'Plan', value: 1 },
        ],
      }),
    ),
  );
  await expect(page.getByTestId('stack-value').first()).toHaveText('2.5 wks');
  await expect(segments(page).first()).toContainText('New');
});

test('side left puts the stack on the left', async ({ mount, page }) => {
  await mount(render(stackSlide({ ...base, side: 'left' })));
  const figure = (await box(page, 'stack-figure'))!;
  const text = (await box(page, 'stack-text'))!;
  expect(figure.x + figure.width).toBeLessThanOrEqual(text.x);
  expect(figure.x).toBeCloseTo(120, 0);
  expect(figure.width).toBeCloseTo(820, 0);
  expect(text.width).toBeCloseTo(800, 0);
});

test('side right is the default', async ({ mount, page }) => {
  await mount(render(stackSlide(base)));
  const figure = (await box(page, 'stack-figure'))!;
  const text = (await box(page, 'stack-text'))!;
  expect(text.x + text.width).toBeLessThanOrEqual(figure.x);
  expect(figure.x + figure.width).toBeCloseTo(1800, 0);
});

test('no points centres a 900 px figure', async ({ mount, page }) => {
  await mount(render(stackSlide({ ...base, points: undefined })));
  const figure = (await box(page, 'stack-figure'))!;
  expect(figure.width).toBeCloseTo(900, 0);
  expect(figure.x + figure.width / 2).toBeCloseTo(960, 0);
  await expect(page.getByTestId('stack-text')).toHaveCount(0);
  expect((await box(page, 'stack-column'))!.height).toBeCloseTo(500, 0);
});

test('the figure has one sentence for screen readers', async ({ mount, page }) => {
  await mount(
    render(
      stackSlide({
        ...base,
        unit: 'ms',
        total: 'Whole request',
        segments: [
          { label: 'Edge', value: 10 },
          { label: 'Core', value: 30 },
        ],
      }),
    ),
  );
  await expect(page.getByTestId('stack-figure')).toHaveAttribute(
    'aria-label',
    'Stack of 2 segments, top to bottom: Edge 10 ms; Core 30 ms, making Whole request.',
  );
});

test('a callout that reveals last arrives after the segments', async ({ mount, page }) => {
  const slots: StackSlots = {
    ...base,
    reveal: 'one-by-one',
    callout: { tone: 'accent', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(stackSlide(slots), 3));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(stackSlide(slots), 4));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      stackSlide({
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

// Every text string widened; options and ids are left as they are.
const NOT_TEXT = new Set(['reveal', 'tone', 'fill', 'in', 'side']);
function widen(value: unknown, key?: string): unknown {
  if (typeof value === 'string')
    return key !== undefined && NOT_TEXT.has(key) ? value : wide(value.length);
  if (Array.isArray(value)) return value.map((item) => widen(item));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, widen(v, k)]));
  }
  return value;
}

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-stack').evaluate((root: Element) => {
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

// The stack's own height, the bottom of the figure, and the side's height: the
// points list plus the callout, which the server rule holds to 620 px.
async function heights(page: Page) {
  const stack = (await box(page, 'stack-column'))!.height;
  const figure = await boxBottom(page, 'stack-figure');
  const list = page.getByTestId('stack-point');
  const listed =
    (await list.count()) > 0
      ? await list.evaluateAll((els) => {
          const first = els[0].getBoundingClientRect();
          const last = els[els.length - 1].getBoundingClientRect();
          return last.bottom - first.top;
        })
      : 0;
  const callout =
    (await page.getByTestId('body-callout').count()) > 0
      ? (await box(page, 'body-callout'))!.height
      : 0;
  const rows = await segments(page).evaluateAll((els) =>
    els.map((el) => Math.round(el.getBoundingClientRect().height)),
  );
  return { stack, figure, side: Math.round(listed + callout), rows };
}

for (const name of ['stackSeven', 'stackEight']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = fitFixture(name).slots as StackSlots;
    // Every step shown, so a callout that reveals last is drawn.
    await mount(render(stackSlide(slots), 20));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    const h = await heights(page);
    expect(h.stack).toBeCloseTo(500, 0);
    expect(h.figure).toBeLessThanOrEqual(996);
    expect(h.side).toBeLessThanOrEqual(620);
    // Every segment's text stays inside its row.
    const overflow = await segments(page).evaluateAll(
      (rows) => rows.filter((row) => row.scrollHeight > row.clientHeight + 1).length,
    );
    expect(overflow).toBe(0);
    test.info().annotations.push({ type: 'heights', description: JSON.stringify(h) });
  });

  test(`${name} wide twin stays between x 120 and 1800`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = widen(fitFixture(name).slots) as StackSlots;
    await mount(render(stackSlide(slots), 20));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    // The bottom is not asserted: wide letters wrap sooner than the estimate counts.
    const h = await heights(page);
    test
      .info()
      .annotations.push({ type: 'bottom', description: `${drawn.bottom} ${JSON.stringify(h)}` });
  });
}

test('screenshot: stack with points', async ({ mount }) => {
  const component = await mount(
    render(
      stackSlide({
        ...base,
        unit: 'wks',
        total: 'Ten weeks end to end',
        caption: 'Time from idea to customer',
        segments: [
          { label: 'Release', value: 4 },
          { label: 'Review', value: 3 },
          { label: 'Build', value: 2 },
          { label: 'Plan', value: 1 },
        ],
        points: [
          { lead: 'Faster', text: 'Feedback arrives sooner.' },
          { lead: 'Safer', text: 'A small change is easy to undo.' },
        ],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-stack-with-points.png');
});

test('screenshot: stack toned segment', async ({ mount }) => {
  const component = await mount(
    render(
      stackSlide({
        ...base,
        segments: [
          { label: 'Customers', note: 'Where it hurts', tone: 'negative', tag: 'Hot' },
          { label: 'Services', note: 'Where it starts' },
          { label: 'Storage' },
        ],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-stack-toned-segment.png');
});
