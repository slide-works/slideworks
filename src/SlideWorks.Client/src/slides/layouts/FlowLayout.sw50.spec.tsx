import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { FlowSlots } from '../slots/flow';
import type { SlideDeck } from '../slots';

// The flow layout: steps that keep their space, an arrow that arrives with the
// node it points to, the box of tiles at twice the width, nodes of one height,
// the figure's description, the clipped figure and the two fit fixtures.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function flowSlide(slots: FlowSlots, number = 12): RenderableSlide {
  return { number, layout: 'flow', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const base: FlowSlots = {
  kicker: 'The road',
  headline: 'Every record takes the same short road',
  nodes: [
    { title: 'Collect', text: 'Gather what arrived.' },
    { title: 'Clean', text: 'Fix each row in turn, then check it twice over.' },
    { title: 'Publish' },
  ],
};

const nodes = (page: Page) => page.getByTestId('flow-node');
const rects = (page: Page, testId: string) =>
  page.getByTestId(testId).evaluateAll((els) =>
    els.map((el) => {
      const r = el.getBoundingClientRect();
      return { x: r.x, y: r.y, width: r.width, height: r.height };
    }),
  );

test('hidden nodes keep their space', async ({ mount, page }) => {
  const slots: FlowSlots = { ...base, reveal: 'one-by-one' };
  const component = await mount(render(flowSlide(slots), 1));
  const reveals = page.getByTestId('flow-figure').locator('.sw-reveal');
  const nodeReveals = nodes(page).locator('xpath=..');
  await expect(nodeReveals.nth(0)).toHaveAttribute('data-revealed', 'true');
  await expect(nodeReveals.nth(1)).toHaveAttribute('data-revealed', 'false');
  await expect(nodeReveals.nth(2)).toHaveAttribute('data-revealed', 'false');
  await expect(reveals).toHaveCount(5);
  const before = await rects(page, 'flow-node');
  await component.update(render(flowSlide(slots), 3));
  await expect(nodeReveals.nth(2)).toHaveAttribute('data-revealed', 'true');
  expect(await rects(page, 'flow-node')).toEqual(before);
});

test('an arrow arrives with the node it points to', async ({ mount, page }) => {
  const slots: FlowSlots = { ...base, reveal: 'one-by-one' };
  const component = await mount(render(flowSlide(slots), 1));
  const arrows = page.getByTestId('flow-arrow').locator('xpath=..');
  const nodeReveals = nodes(page).locator('xpath=..');
  await expect(arrows).toHaveCount(2);
  await expect(arrows.nth(0)).toHaveAttribute('data-revealed', 'false');
  await component.update(render(flowSlide(slots), 2));
  await expect(arrows.nth(0)).toHaveAttribute('data-revealed', 'true');
  await expect(nodeReveals.nth(1)).toHaveAttribute('data-revealed', 'true');
  await expect(arrows.nth(1)).toHaveAttribute('data-revealed', 'false');
  await component.update(render(flowSlide(slots), 3));
  await expect(arrows.nth(1)).toHaveAttribute('data-revealed', 'true');
  await expect(page.getByTestId('flow-arrow').first()).toHaveAttribute('aria-hidden', 'true');
  const arrow = (await rects(page, 'flow-arrow'))[0];
  expect(arrow.width).toBeCloseTo(56, 0);
  expect(arrow.height).toBeCloseTo(28, 0);
});

test('a group node is twice as wide and draws tiles', async ({ mount, page }) => {
  await mount(
    render(
      flowSlide({
        ...base,
        nodes: [
          { title: 'Collect' },
          { title: 'Clean', items: ['Verify', 'Strip', 'Resize'] },
          { title: 'Publish' },
        ],
      }),
    ),
  );
  const [first, group, last] = await rects(page, 'flow-node');
  // (1680 - 72 x 2) / 4 = 384 px for a plain node, 768 px for the group.
  expect(first.width).toBeCloseTo(384, 0);
  expect(last.width).toBeCloseTo(384, 0);
  expect(group.width).toBeCloseTo(768, 0);
  await expect(page.getByTestId('flow-tile')).toHaveCount(3);
  const tiles = await rects(page, 'flow-tile');
  expect(tiles[0].width).toBeCloseTo((768 - 60 - 14) / 2, 0);
  expect(tiles[1].x - (tiles[0].x + tiles[0].width)).toBeCloseTo(14, 0);
  expect(tiles[2].y - (tiles[0].y + tiles[0].height)).toBeCloseTo(14, 0);
  await expect(nodes(page).nth(1)).toHaveAttribute('data-group', 'true');
});

test('all nodes share the tallest height', async ({ mount, page }) => {
  await mount(
    render(
      flowSlide({
        ...base,
        nodes: [
          { title: 'Short' },
          { title: 'Tall', text: 'Several lines of text sit in this node, making it much taller.' },
          { title: 'Short too', tone: 'inverse' },
        ],
      }),
    ),
  );
  const heights = (await rects(page, 'flow-node')).map((r) => Math.round(r.height));
  expect(heights[0]).toBe(heights[1]);
  expect(heights[2]).toBe(heights[1]);
  expect(heights[1]).toBeGreaterThan(150);
  const tops = (await rects(page, 'flow-node')).map((r) => Math.round(r.y));
  expect(new Set(tops).size).toBe(1);
});

test('the figure has one sentence for screen readers', async ({ mount, page }) => {
  await mount(
    render(
      flowSlide({
        ...base,
        nodes: [
          { title: 'In', label: 'Start' },
          { title: 'Out', text: 'Done', items: ['A', 'B'] },
        ],
      }),
    ),
  );
  await expect(page.getByTestId('flow-figure')).toHaveAttribute(
    'aria-label',
    'Flow of 2 steps, left to right: In (Start); then Out, Done, holding A, B.',
  );
});

test('the figure box clips its overflow', async ({ mount, page }) => {
  await mount(render(flowSlide(base)));
  await expect(page.getByTestId('flow-figure')).toHaveCSS('overflow', 'hidden');
});

test('a callout that reveals last arrives after the nodes', async ({ mount, page }) => {
  const slots: FlowSlots = {
    ...base,
    reveal: 'one-by-one',
    callout: { tone: 'accent', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(flowSlide(slots), 3));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(flowSlide(slots), 4));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      flowSlide({
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

// Every text string widened. Options (tone, reveal, fill, in) stay as they are.
const NOT_TEXT = new Set(['reveal', 'tone', 'fill', 'in']);
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
// past it. `uncut` is the lowest text bottom with no cut, which is where a
// too-wide font would have drawn.
function textBox(page: Page) {
  return page.getByTestId('layout-flow').evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const figure = root.querySelector('[data-testid="flow-figure"]')!.getBoundingClientRect();
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

// The drawn body: from the figure's top to the bottom of the callout (or the
// figure), in px, which is what the server's fit rule estimates.
function bodyHeight(page: Page) {
  return page.getByTestId('flow-body').evaluate((body: Element) => {
    const top = body.querySelector('[data-testid="flow-figure"]')!.getBoundingClientRect().top;
    const last = body.lastElementChild!.getBoundingClientRect();
    return last.bottom - top;
  });
}

// The tallest node as the server's FlowLayout.cs counts it.
const NODE_HEIGHT: Record<string, number> = { flowFour: 311.2, flowGroup: 295 };

for (const name of ['flowFour', 'flowGroup']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    const fixture = fitFixture(name);
    await mount(render(flowSlide(fixture.slots as FlowSlots), 20));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    const height = Math.round(await bodyHeight(page));
    // The server's estimate never counts less than is drawn (it wraps early),
    // and its node height is the drawn one, to a pixel or two.
    expect(height).toBeLessThanOrEqual((fixture.estimate ?? 0) + 2);
    const [node] = await rects(page, 'flow-node');
    expect(Math.abs(node.height - NODE_HEIGHT[name])).toBeLessThanOrEqual(2);
    const overflow = await page
      .getByTestId('flow-figure')
      .evaluate((figure) => figure.scrollWidth - figure.clientWidth);
    expect(overflow).toBe(0);
    test
      .info()
      .annotations.push({ type: 'height', description: `${height} bottom ${drawn.bottom}` });
  });

  test(`${name} wide twin stays between x 120 and 1800`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = widen(fitFixture(name).slots) as FlowSlots;
    await mount(render(flowSlide(slots), 20));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    // The figure clips what a wide font would draw past it, so what shows
    // ends inside the box; `uncut` is how far down it would have drawn.
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    test.info().annotations.push({
      type: 'bottom',
      description: `${drawn.bottom} uncut ${drawn.uncut}`,
    });
  });
}

test('screenshot: flow four', async ({ mount, page }) => {
  await stubApi(page, {});
  const component = await mount(render(flowSlide(fitFixture('flowFour').slots as FlowSlots), 20));
  await expect(component).toHaveScreenshot('layout-flow-four.png');
});

test('screenshot: flow with group', async ({ mount, page }) => {
  await stubApi(page, {});
  const component = await mount(render(flowSlide(fitFixture('flowGroup').slots as FlowSlots), 20));
  await expect(component).toHaveScreenshot('layout-flow-with-group.png');
});
