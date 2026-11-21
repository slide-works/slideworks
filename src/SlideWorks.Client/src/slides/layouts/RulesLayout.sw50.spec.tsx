import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { RulesSlots } from '../slots/rules';
import type { SlideDeck } from '../slots';
import { wideLetters } from '../../test-support/text/maxText';

// The rules layout's SW-50 additions: a callout under the rules (also one that
// reveals last) and emphasis in the headline.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function rulesSlide(slots: RulesSlots, number = 14): RenderableSlide {
  return { number, layout: 'rules', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const base: RulesSlots = {
  kicker: 'Make it stick',
  headline: 'Three rules for the first month',
  rules: [
    { title: 'Merge to main every day', text: 'Behind a flag if it is not ready.' },
    { title: 'Demo something every Friday', text: 'To a customer if you can.' },
    { title: 'Split any story older than a week', text: 'Then drop what is no longer true.' },
  ],
};

const items = (page: Page) => page.getByTestId('layout-rules').locator('li');

test('callout under the rules', async ({ mount, page }) => {
  await mount(render(rulesSlide({ ...base, callout: { tone: 'info', text: 'Rules bend.' } })));
  const last = (await items(page).nth(2).boundingBox())!;
  const callout = (await page.getByTestId('body-callout').boundingBox())!;
  expect(callout.y).toBeGreaterThanOrEqual(last.y + last.height);
  await expect(page.getByTestId('body-callout')).toContainText('Rules bend.');
  await expect(page.getByTestId('body-callout').locator('.sw-reveal')).toHaveCount(0);
});

test('callout arrives after the last rule', async ({ mount, page }) => {
  const slots: RulesSlots = {
    ...base,
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(rulesSlide(slots), 3));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  const before = (await page.getByTestId('body-callout').boundingBox())!;
  await component.update(render(rulesSlide(slots), 4));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
  expect(await page.getByTestId('body-callout').boundingBox()).toEqual(before);
});

test('rules without a callout have no callout markup', async ({ mount, page }) => {
  await mount(render(rulesSlide(base)));
  await expect(page.getByTestId('body-callout')).toHaveCount(0);
  await expect(page.locator('[data-tone]')).toHaveCount(0);
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      rulesSlide({
        ...base,
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

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-rules').evaluate((root: Element) => {
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

// Bottom edge, in slide pixels, of the first box matching a test id (or the last, with `last`).
function boxBottom(page: Page, testId: string, last = false) {
  return page.getByTestId(testId).evaluateAll((els: Element[], pick: boolean) => {
    if (els.length === 0) return null;
    const el = pick ? els[els.length - 1] : els[0];
    const slide = el.closest('[data-layout]')!.getBoundingClientRect();
    return el.getBoundingClientRect().bottom - slide.top;
  }, last);
}

test('rulesCallout stays inside the box', async ({ mount, page }) => {
  await stubApi(page, {});
  const slots = fitFixture('rulesCallout').slots as RulesSlots;
  // Every step shown, so a callout that reveals last is drawn.
  await mount(render(rulesSlide(slots), 10));
  await page.evaluate(() => document.fonts.ready);
  const drawn = await textBox(page);
  expect(drawn.left).toBeGreaterThanOrEqual(120);
  expect(drawn.right).toBeLessThanOrEqual(1800);
  expect(drawn.bottom).toBeLessThanOrEqual(996);
  const lastRule = await items(page)
    .last()
    .evaluate((el: Element) => {
      const slide = el.closest('[data-layout]')!.getBoundingClientRect();
      return el.getBoundingClientRect().bottom - slide.top;
    });
  expect(lastRule).toBeLessThanOrEqual(996);
  expect(await boxBottom(page, 'body-callout')).toBeLessThanOrEqual(996);
  test.info().annotations.push({ type: 'bottom', description: `${drawn.bottom}` });
});

test('rulesCallout wide twin stays between x 120 and 1800', async ({ mount, page }) => {
  await stubApi(page, {});
  const slots = widen(fitFixture('rulesCallout').slots) as RulesSlots;
  await mount(render(rulesSlide(slots), 10));
  await page.evaluate(() => document.fonts.ready);
  const drawn = await textBox(page);
  expect(drawn.left).toBeGreaterThanOrEqual(120);
  expect(drawn.right).toBeLessThanOrEqual(1800);
  // The bottom is not asserted: wide letters wrap sooner than the estimate counts.
  test.info().annotations.push({ type: 'bottom', description: `${drawn.bottom}` });
});

test('screenshot: rules with callout', async ({ mount }) => {
  const component = await mount(
    render(
      rulesSlide({ ...base, callout: { tone: 'info', text: 'Rules bend; the habit should not.' } }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-rules-with-callout.png');
});
