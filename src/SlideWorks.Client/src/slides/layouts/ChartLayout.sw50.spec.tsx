import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { ChartSlots } from '../slots/chart';
import type { SlideDeck } from '../slots';

// The chart layout's SW-50 additions: a callout under the chart, which the
// plot shrinks for, and emphasis in the headline.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

// In a real part, so the tracker draws as it would in a deck.
function slideOf(slots: ChartSlots): RenderableSlide {
  return { number: 3, layout: 'chart', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const box = (page: Page, testId: string) => page.getByTestId(testId).boundingBox();

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-chart').evaluate((root: Element) => {
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

const base: ChartSlots = {
  headline: 'Lead time fell as the slices got smaller',
  kind: 'bar',
  xLabels: ['Jul', 'Aug', 'Sep', 'Oct'],
  series: [{ name: 'Lead time (days)', values: [24, 18, 9, 6] }],
};

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(render(slideOf({ ...base, emphasis: [{ text: 'fell', tone: 'positive' }] })));
  const phrase = page.getByTestId('chart-headline').getByTestId('emphasis');
  await expect(phrase).toHaveText('fell');
  await expect(phrase).toHaveAttribute('data-tone', 'positive');
});

test('a slide without a callout draws no wrapper', async ({ mount, page }) => {
  await mount(render(slideOf(base)));
  await expect(page.getByTestId('body-callout')).toHaveCount(0);
  await expect(page.getByTestId('chart-body').locator('xpath=./div')).toHaveCount(1);
});

test('callout arrives on the last step', async ({ mount, page }) => {
  const slots: ChartSlots = { ...base, callout: { tone: 'info', text: 'Last', reveal: 'last' } };
  const component = await mount(render(slideOf(slots), 0));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(slideOf(slots), 1));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

test('chart shrinks above a callout', async ({ mount, page }) => {
  const slots = fitFixture('chartCallout').slots as ChartSlots;
  await mount(render(slideOf({ ...slots, callout: { ...slots.callout!, reveal: undefined } })));
  await page.evaluate(() => document.fonts.ready);
  const callout = (await box(page, 'body-callout'))!;
  expect(callout.height).toBeGreaterThan(120);
  const plot = (await box(page, 'chart-plot'))!;
  expect(plot.height).toBeGreaterThanOrEqual(300);
  expect(plot.y + plot.height).toBeLessThanOrEqual(callout.y);
  expect(callout.y + callout.height).toBeLessThanOrEqual(996);
});

test('chartCallout and its wide twin stay inside the box', async ({ mount, page }) => {
  await stubApi(page, {});
  for (const name of ['chartCallout', 'chartCalloutWide']) {
    const slots = fitFixture(name).slots as ChartSlots;
    const component = await mount(render(slideOf(slots), 1));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left, name).toBeGreaterThanOrEqual(120);
    expect(drawn.right, name).toBeLessThanOrEqual(1800);
    expect(drawn.bottom, name).toBeLessThanOrEqual(996);
    const plot = (await box(page, 'chart-plot'))!;
    expect(plot.height, name).toBeGreaterThanOrEqual(300);
    await component.unmount();
  }
});

test('screenshot: chart with callout', async ({ mount }) => {
  const component = await mount(
    render(
      slideOf({
        kicker: 'Ship it',
        headline: 'Lead time fell as the slices got smaller',
        kind: 'bar',
        xLabels: ['Jul', 'Aug', 'Sep', 'Oct'],
        series: [{ name: 'Lead time (days)', values: [24, 18, 9, 6] }],
        yUnit: 'days',
        caption: 'Median lead time per month.',
        callout: { tone: 'good', text: 'Smaller slices, faster feedback.' },
        emphasis: [{ text: 'fell', tone: 'positive' }],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-chart-callout.png');
});
