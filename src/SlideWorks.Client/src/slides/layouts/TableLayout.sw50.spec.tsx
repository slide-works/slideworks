import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { TableSlots } from '../slots/table';
import type { SlideDeck } from '../slots';
import { wideLetters } from '../../test-support/text/maxText';

// The table layout's SW-50 additions: rows that arrive one per step, a
// strike-and-answer reveal, a callout, emphasis in the headline and cells of 80.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function tableSlide(slots: TableSlots, number = 11): RenderableSlide {
  return { number, layout: 'table', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const strike: TableSlots = {
  kicker: 'Myths',
  headline: 'Claims, then the answers',
  columns: ['Claim', 'Answer'],
  rows: [
    ['Bigger batches are cheaper', 'Smaller ones ship sooner'],
    ['Plans do not change', 'Plans change weekly'],
    ['Quality costs time', 'Quality saves time'],
  ],
  reveal: 'strike-and-answer',
};

const rowsOf = (page: Page) => page.getByTestId('table-row');

test('step 2 strikes row 2 and dims row 1', async ({ mount, page }) => {
  await mount(render(tableSlide(strike), 2));
  const rows = rowsOf(page);
  const left = (i: number) => rows.nth(i).getByTestId('table-cell').first();
  await expect(left(0)).toHaveAttribute('data-struck', 'true');
  await expect(left(1)).toHaveAttribute('data-struck', 'true');
  await expect(left(2)).not.toHaveAttribute('data-struck');
  await expect(left(1)).toHaveCSS('text-decoration-line', 'line-through');
  await expect(rows.nth(0)).toHaveAttribute('data-dim', 'true');
  await expect(rows.nth(1)).not.toHaveAttribute('data-dim');
  await expect(rows.nth(2)).not.toHaveAttribute('data-dim');
  await expect(rows.nth(0)).toHaveCSS('opacity', '0.45');
  const answers = rows.locator('.sw-reveal');
  await expect(answers).toHaveCount(3);
  await expect(answers.nth(1)).toHaveAttribute('data-revealed', 'true');
  await expect(answers.nth(2)).toHaveAttribute('data-revealed', 'false');
});

test('step 0 hides every answer and keeps its space', async ({ mount, page }) => {
  const component = await mount(render(tableSlide(strike), 0));
  await page.evaluate(() => document.fonts.ready);
  const answers = rowsOf(page).locator('.sw-reveal');
  await expect(answers).toHaveCount(3);
  const before = await Promise.all([0, 1, 2].map((i) => answers.nth(i).boundingBox()));
  const rowBoxes = await Promise.all([0, 1, 2].map((i) => rowsOf(page).nth(i).boundingBox()));
  for (let i = 0; i < 3; i++) {
    await expect(answers.nth(i)).toHaveAttribute('data-revealed', 'false');
    await expect(answers.nth(i)).toHaveCSS('visibility', 'hidden');
    // Every left cell shows from the start, and none is struck.
    await expect(rowsOf(page).nth(i).getByTestId('table-cell').first()).not.toHaveAttribute(
      'data-struck',
    );
  }
  await component.update(render(tableSlide(strike), 3));
  for (let i = 0; i < 3; i++) {
    await expect(answers.nth(i)).toHaveAttribute('data-revealed', 'true');
    expect(await answers.nth(i).boundingBox()).toEqual(before[i]);
    expect(await rowsOf(page).nth(i).boundingBox()).toEqual(rowBoxes[i]);
  }
});

test('one-by-one shows row n at step n', async ({ mount, page }) => {
  const slots: TableSlots = { ...strike, reveal: 'one-by-one' };
  const component = await mount(render(tableSlide(slots), 0));
  const cells = rowsOf(page).locator('.sw-reveal');
  // Every cell of a row is wrapped: three rows of two cells.
  await expect(cells).toHaveCount(6);
  for (let step = 0; step <= 3; step++) {
    await component.update(render(tableSlide(slots), step));
    for (let r = 0; r < 3; r++) {
      for (let c = 0; c < 2; c++) {
        await expect(cells.nth(r * 2 + c)).toHaveAttribute('data-revealed', String(step >= r + 1));
      }
      await expect(rowsOf(page).nth(r)).not.toHaveAttribute('data-dim');
    }
  }
  await expect(page.locator('[data-struck]')).toHaveCount(0);
});

test('a table without reveal has no reveal wrappers', async ({ mount, page }) => {
  await mount(render(tableSlide({ ...strike, reveal: undefined })));
  await expect(page.locator('.sw-reveal')).toHaveCount(0);
  await expect(page.locator('[data-struck], [data-dim]')).toHaveCount(0);
});

test('callout arrives after the last row', async ({ mount, page }) => {
  const slots: TableSlots = {
    ...strike,
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(tableSlide(slots), 3));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  const table = (await page.locator('table').boundingBox())!;
  const before = (await page.getByTestId('body-callout').boundingBox())!;
  expect(before.y).toBeGreaterThanOrEqual(table.y + table.height);
  await component.update(render(tableSlide(slots), 4));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
  expect(await page.getByTestId('body-callout').boundingBox()).toEqual(before);
});

test('a callout without reveal shows from the start', async ({ mount, page }) => {
  await mount(render(tableSlide({ ...strike, callout: { tone: 'info', text: 'Now' } })));
  await expect(page.getByTestId('body-callout').locator('.sw-reveal')).toHaveCount(0);
  await expect(page.getByTestId('body-callout')).toContainText('Now');
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      tableSlide({
        ...strike,
        reveal: undefined,
        headline: 'Small steps beat big plans',
        emphasis: [{ text: 'big plans', tone: 'negative' }],
      }),
    ),
  );
  const phrase = page.getByTestId('emphasis');
  await expect(phrase).toHaveText('big plans');
  await expect(phrase).toHaveAttribute('data-tone', 'negative');
});

// Wide letters (capitals, W and M) of exactly `length` characters. Copied from
// DataLayouts.fixtures.ts, which is frozen.
const WIDE_TEXT = 'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';
const wide = (length: number): string => wideLetters(length, WIDE_TEXT);

// Every text string widened; ids and options (reveal, tone) are left as they are.
const NOT_TEXT = new Set(['reveal', 'tone', 'in']);
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
  return page.getByTestId('layout-table').evaluate((root: Element) => {
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

for (const name of ['tableCallout', 'tableCells80']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = fitFixture(name).slots as TableSlots;
    // Every step shown, so a callout that reveals last is drawn.
    await mount(render(tableSlide(slots), 10));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    test.info().annotations.push({ type: 'bottom', description: `${drawn.bottom}` });
  });

  test(`${name} wide twin stays between x 120 and 1800`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = widen(fitFixture(name).slots) as TableSlots;
    await mount(render(tableSlide(slots), 10));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    // The bottom is not asserted: the server refuses this wide twin's rows (allW), so no legal slide draws it.
    test.info().annotations.push({ type: 'bottom', description: `${drawn.bottom}` });
  });
}

test('screenshot: table strike step 2', async ({ mount }) => {
  const component = await mount(render(tableSlide(strike), 2));
  await expect(component).toHaveScreenshot('layout-table-strike-step-2.png');
});
