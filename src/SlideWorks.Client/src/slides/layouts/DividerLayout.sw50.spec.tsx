import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPart } from '../../api/parts';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { DividerSlots, SlideDeck } from '../slots';

// The divider's SW-50 additions: parts as objects (short names, captions, marks)
// in the tracker, the journey and the divider, a journey that shrinks its names
// before it breaks a word, and a sub of 110 characters.

test.use({ viewport: { width: 1920, height: 1080 } });

const PARTS: DeckPart[] = [
  { name: 'The cost of big batches', short: 'Cost', caption: 'Why we slip', mark: null },
  { name: 'Slice it thin', short: 'Slice', caption: null, mark: null },
  { name: 'Ship it every week', short: 'Ship', caption: 'The habit', mark: null },
  { name: 'Appendix', short: 'Appx', caption: null, mark: 'A' },
];

function deckOf(parts: DeckPart[] = PARTS): SlideDeck {
  return {
    title: 'Ship smaller',
    author: 'Sam Rivera',
    parts,
    look: lookViewOf('talk', 'cobalt'),
    deepDives: [],
  };
}

function slideOf(layout: 'bullets' | 'divider', part: number | null): RenderableSlide {
  return layout === 'divider'
    ? {
        number: 7,
        layout,
        part,
        deepDive: false,
        slots: { title: 'Slice it thin', sub: 'Cut work so each piece ships.' },
      }
    : {
        number: 8,
        layout,
        part,
        deepDive: false,
        slots: { headline: 'Small batches', items: ['One', 'Two'] },
      };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, deck: SlideDeck = deckOf()) {
  return <SlideRenderer slide={slide} deck={deck} total={16} step={0} />;
}

function dividerOf(slots: DividerSlots, part: number): RenderableSlide {
  return { number: 7, layout: 'divider', part, deepDive: false, slots };
}

const eight = (name: (i: number) => string): DeckPart[] =>
  Array.from({ length: 8 }, (_, i) => ({
    name: name(i),
    short: `P${i + 1}`,
    caption: null,
    mark: null,
  }));

// The extent of every run of text in the divider, in canvas px from the slide's top left.
async function textBox(page: Page) {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId('layout-divider').evaluate((root: Element) => {
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

// The size of the journey's names, and how many lines of text each draws.
async function nameMetrics(page: Page) {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId('journey-part').evaluateAll((items) =>
    items.map((item) => {
      const name = item.querySelector('b')!;
      const range = document.createRange();
      range.selectNodeContents(name);
      const lines = new Set([...range.getClientRects()].map((rect) => Math.round(rect.top)));
      return { size: parseFloat(getComputedStyle(name).fontSize), lines: lines.size };
    }),
  );
}

test('tracker uses short names', async ({ mount, page }) => {
  await mount(render(slideOf('bullets', 2)));

  await expect(page.getByTestId('part-tracker-part')).toHaveText(['Cost', 'Slice', 'Ship']);
  await expect(page.getByTestId('part-tracker-part').nth(1)).toHaveAttribute(
    'aria-current',
    'step',
  );
  await expect(page.locator('[aria-current]')).toHaveCount(1);
});

test('slide with no part has no tracker', async ({ mount, page }) => {
  await mount(render(slideOf('bullets', null)));

  await expect(page.getByTestId('slide-frame')).toBeAttached();
  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
});

test('a slide in a marked part has no tracker', async ({ mount, page }) => {
  await mount(render(slideOf('bullets', 4)));

  await expect(page.getByTestId('slide-frame')).toBeAttached();
  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
});

test('divider shows captions under the stops', async ({ mount, page }) => {
  await mount(render(slideOf('divider', 1)));

  const stops = page.getByTestId('journey-part');
  await expect(stops).toHaveCount(3);
  await expect(stops.nth(0)).toContainText('Why we slip');
  await expect(stops.nth(2)).toContainText('The habit');
  await expect(stops.locator('b')).toHaveText(PARTS.slice(0, 3).map((part) => part.name));
});

test('a stop without a caption says Part n', async ({ mount, page }) => {
  await mount(render(slideOf('divider', 1)));

  await expect(page.getByTestId('journey-part').nth(1)).toContainText('Part 2');
  await expect(page.getByTestId('journey-part').nth(1)).not.toContainText('Why we slip');
});

test('a marked divider shows its mark and no journey', async ({ mount, page }) => {
  const deck = { ...deckOf(), deepDives: [{ part: 4, number: 9, headline: 'Extra' }] };
  await mount(render(slideOf('divider', 4), deck));

  await expect(page.getByTestId('divider-number')).toHaveText('A');
  await expect(page.getByTestId('divider-title')).toHaveText('Slice it thin');
  await expect(page.getByTestId('block-journey')).toHaveCount(0);
  await expect(page.getByTestId('divider-deep-dives')).toHaveCount(0);
});

test('a divider in a deck where every part is marked draws no journey', async ({ mount, page }) => {
  const parts = PARTS.map((part) => ({ ...part, mark: 'A' }));
  await mount(render(slideOf('divider', 1), deckOf(parts)));

  await expect(page.getByTestId('layout-divider')).toBeAttached();
  await expect(page.getByTestId('block-journey')).toHaveCount(0);
});

test('17 capitals with eight stops do not break', async ({ mount, page }) => {
  const parts = eight(() => 'REALITYSTRESSTEST');
  await mount(render(slideOf('divider', 3), deckOf(parts)));

  await expect
    .poll(async () => (await nameMetrics(page)).map((name) => name.size))
    .toEqual(Array.from({ length: 8 }, () => 13));
  for (const name of await nameMetrics(page)) expect(name.lines).toBe(1);
});

test('a 30-letter word breaks only at 13 px and stays inside the slide', async ({
  mount,
  page,
}) => {
  const parts = eight((i) => (i === 0 ? 'W'.repeat(30) : 'Plain'));
  await mount(render(slideOf('divider', 3), deckOf(parts)));

  await expect
    .poll(async () => (await nameMetrics(page)).map((name) => name.size))
    .toEqual(Array.from({ length: 8 }, () => 13));
  const [long] = await nameMetrics(page);
  expect(long!.lines).toBeGreaterThan(1);
  await expect(page.getByTestId('journey-part').first().locator('b')).toHaveAttribute(
    'title',
    'W'.repeat(30),
  );
  const box = await textBox(page);
  expect(box.left).toBeGreaterThanOrEqual(120);
  expect(box.right).toBeLessThanOrEqual(1800);
});

test('names that fit keep the full size', async ({ mount, page }) => {
  await mount(render(slideOf('divider', 1)));

  await expect
    .poll(async () => (await nameMetrics(page)).map((name) => name.size))
    .toEqual([18, 18, 18]);
});

for (const name of ['dividerSub', 'dividerSubWide']) {
  test(`a 110-character sub with eight parts stays above y 996: ${name}`, async ({
    mount,
    page,
  }) => {
    const slots = fitFixture(name).slots as DividerSlots;
    expect(slots.sub).toHaveLength(110);
    await mount(
      render(dividerOf(slots, 8), deckOf(eight((i) => `Part ${i + 1} of the long road`))),
    );

    const box = await textBox(page);
    expect(box.left).toBeGreaterThanOrEqual(120);
    expect(box.right).toBeLessThanOrEqual(1800);
    expect(box.bottom).toBeLessThanOrEqual(996);
  });
}

test('screenshot: divider captions', async ({ mount, page }) => {
  const component = await mount(render(slideOf('divider', 1)));
  await page.evaluate(() => document.fonts.ready);
  await expect(component).toHaveScreenshot('divider-captions.png');
});

test('screenshot: divider mark', async ({ mount, page }) => {
  const component = await mount(render(slideOf('divider', 4)));
  await page.evaluate(() => document.fonts.ready);
  await expect(component).toHaveScreenshot('divider-mark.png');
});

test('screenshot: divider eight parts', async ({ mount, page }) => {
  const parts = eight((i) => `STAKEHOLDERS ${i + 1} PLANS`);
  const component = await mount(render(slideOf('divider', 5), deckOf(parts)));
  await page.evaluate(() => document.fonts.ready);
  await expect(component).toHaveScreenshot('divider-eight-parts.png');
});
