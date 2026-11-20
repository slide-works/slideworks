import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { GanttSlots } from '../slots/gantt';
import type { SlideDeck } from '../slots';

// The gantt layout's SW-50 additions: a callout under the timeline, which the
// rows shrink for, and emphasis in the headline.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

// In a real part, so the tracker draws as it would in a deck.
function slideOf(slots: GanttSlots): RenderableSlide {
  return { number: 3, layout: 'gantt', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const box = (page: Page, testId: string) => page.getByTestId(testId).boundingBox();

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-gantt').evaluate((root: Element) => {
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

const base: GanttSlots = {
  headline: 'One milestone each looks fair',
  columns: ['S1', 'S2', 'S3', 'S4'],
  rows: [
    { label: 'Checkout API', start: 1, end: 2, status: 'done' },
    { label: 'Checkout UI', start: 2, end: 4, status: 'slips', note: 'Waited on the API' },
  ],
};

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(render(slideOf({ ...base, emphasis: [{ text: 'One milestone', tone: 'accent' }] })));
  const phrase = page.getByTestId('gantt-headline').getByTestId('emphasis');
  await expect(phrase).toHaveText('One milestone');
  await expect(phrase).toHaveAttribute('data-tone', 'accent');
});

test('a slide without a callout draws no wrapper', async ({ mount, page }) => {
  await mount(render(slideOf(base)));
  await expect(page.getByTestId('body-callout')).toHaveCount(0);
  await expect(page.getByTestId('gantt-body').locator('xpath=./figure')).toHaveCount(1);
});

test('callout arrives on the last step', async ({ mount, page }) => {
  const slots: GanttSlots = { ...base, callout: { tone: 'info', text: 'Last', reveal: 'last' } };
  const component = await mount(render(slideOf(slots), 0));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(slideOf(slots), 1));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

// The rows' area: from the first column head to the last bar or label.
async function rowsHeight(page: Page): Promise<number> {
  return page.getByTestId('gantt-body').evaluate((body) => {
    const items = [
      ...body.querySelectorAll('[data-testid="gantt-column"], [data-testid="gantt-bar"]'),
    ];
    const top = Math.min(...items.map((e) => e.getBoundingClientRect().top));
    const bottom = Math.max(...items.map((e) => e.getBoundingClientRect().bottom));
    return bottom - top;
  });
}

test('gantt rows shrink above a callout', async ({ mount, page }) => {
  const slots = fitFixture('ganttCallout').slots as GanttSlots;
  await mount(render(slideOf({ ...slots, callout: { ...slots.callout!, reveal: undefined } })));
  await page.evaluate(() => document.fonts.ready);
  const callout = (await box(page, 'body-callout'))!;
  expect(callout.height).toBeGreaterThan(120);
  expect(await rowsHeight(page)).toBeGreaterThanOrEqual(300);
  const caption = (await box(page, 'gantt-caption'))!;
  expect(caption.y + caption.height).toBeLessThanOrEqual(callout.y);
  expect(callout.y + callout.height).toBeLessThanOrEqual(996);
});

test('ganttCallout and its wide twin stay inside the box', async ({ mount, page }) => {
  await stubApi(page, {});
  for (const name of ['ganttCallout', 'ganttCalloutWide']) {
    const slots = fitFixture(name).slots as GanttSlots;
    const component = await mount(render(slideOf(slots), 1));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left, name).toBeGreaterThanOrEqual(120);
    expect(drawn.right, name).toBeLessThanOrEqual(1800);
    expect(drawn.bottom, name).toBeLessThanOrEqual(996);
    expect(await rowsHeight(page), name).toBeGreaterThanOrEqual(300);
    await component.unmount();
  }
});

test('screenshot: gantt with callout', async ({ mount }) => {
  const component = await mount(
    render(
      slideOf({
        kicker: 'The cost',
        headline: 'One milestone each looks fair',
        columns: ['S1', 'S2', 'S3', 'S4'],
        rows: [
          { label: 'Checkout API', start: 1, end: 2, status: 'done' },
          { label: 'Checkout UI', start: 2, end: 4, status: 'slips', note: 'Waited on the API' },
        ],
        caption: 'Everything is green until it isn’t.',
        callout: { tone: 'bad', text: 'The UI waited on the API for two sprints.' },
        emphasis: [{ text: 'One milestone', tone: 'accent' }],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-gantt-callout.png');
});
