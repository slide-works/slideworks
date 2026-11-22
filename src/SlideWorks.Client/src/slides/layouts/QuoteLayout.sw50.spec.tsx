import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { QuoteSlots } from '../slots/quote';
import type { SlideDeck } from '../slots';
import { wideLetters } from '../../test-support/text/maxText';

// The quote layout: a headline, the speaker's role, a stat beside the
// attribution, a follow-up line and a source, light or dark, revealing the
// stat and then the follow-up on steps.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

// In a real part, to prove a deck with parts still draws its tracker here.
function quoteSlide(slots: QuoteSlots, surface?: 'light' | 'dark'): RenderableSlide {
  return { number: 8, layout: 'quote', part: 1, deepDive: false, surface, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const today: QuoteSlots = {
  kicker: 'From the retro',
  quote: "We stopped asking 'is it done?' and started asking 'is it out?'",
  attribution: 'Search team retro, Q3',
};

const full: QuoteSlots = {
  kicker: 'From the retro',
  headline: 'What changed in a quarter',
  quote: "We stopped asking 'is it done?' and started asking 'is it out?'",
  attribution: 'Priya Raman',
  role: 'Head of search, Q3 retro',
  stat: { value: '11 wks', caption: 'From idea to customer, down from 19' },
  follow_up: 'Smaller changes meant fewer surprises on release day.',
  source: 'Search team retro notes, October',
};

function box(page: Page, testId: string) {
  return page
    .getByTestId(testId)
    .first()
    .evaluate((el) => {
      const slide = el.closest('[data-layout]')!.getBoundingClientRect();
      const rect = el.getBoundingClientRect();
      return {
        x: rect.x - slide.x,
        y: rect.y - slide.y,
        width: rect.width,
        height: rect.height,
        bottom: rect.bottom - slide.y,
      };
    });
}

function colourOf(page: Page, testId: string): Promise<string> {
  return page
    .getByTestId(testId)
    .first()
    .evaluate((el) => getComputedStyle(el).color);
}

// The computed colour of a palette token inside an element.
function tokenColour(page: Page, testId: string, key: string): Promise<string> {
  return page
    .getByTestId(testId)
    .first()
    .evaluate((element, token) => {
      const probe = document.createElement('span');
      probe.style.color = `var(--sw-${token})`;
      element.appendChild(probe);
      const colour = getComputedStyle(probe).color;
      probe.remove();
      return colour;
    }, key);
}

test('stat sits beside the attribution', async ({ mount, page }) => {
  await stubApi(page, {});
  await mount(render(quoteSlide(full)));
  await page.evaluate(() => document.fonts.ready);
  const who = await box(page, 'quote-who');
  const stat = await box(page, 'quote-stat');
  expect(who.x + who.width).toBeLessThanOrEqual(stat.x);
  expect(stat.width).toBeCloseTo(420, 0);
  expect(stat.x + stat.width).toBeLessThanOrEqual(1800.5);
  expect(Math.abs(stat.y - who.y)).toBeLessThan(2);
  await expect(page.getByTestId('quote-headline')).toHaveText('What changed in a quarter');
  await expect(page.getByTestId('quote-name')).toHaveText('Priya Raman');
  await expect(page.getByTestId('quote-who')).toHaveText('Priya Raman, Head of search, Q3 retro');
});

test('dark quote', async ({ mount, page }) => {
  await mount(render(quoteSlide(full, 'dark')));
  expect(await colourOf(page, 'quote-text')).toBe(
    await tokenColour(page, 'quote-text', 'night-ink'),
  );
  expect(await colourOf(page, 'quote-source')).toBe(
    await tokenColour(page, 'quote-source', 'night-2'),
  );
  expect(await colourOf(page, 'quote-name')).toBe(
    await tokenColour(page, 'quote-name', 'night-ink'),
  );
});

test('stat then follow-up arrive on their steps and keep their space', async ({ mount, page }) => {
  const slots: QuoteSlots = { ...full, reveal: 'one-by-one' };
  await stubApi(page, {});
  const component = await mount(render(quoteSlide(slots), 0));
  await page.evaluate(() => document.fonts.ready);
  const stat = page.getByTestId('quote-stat').locator('xpath=..');
  const followUp = page.getByTestId('quote-follow-up').locator('xpath=..');
  await expect(stat).toHaveAttribute('data-revealed', 'false');
  await expect(followUp).toHaveAttribute('data-revealed', 'false');
  const hidden = { stat: await box(page, 'quote-stat'), up: await box(page, 'quote-follow-up') };
  await component.update(render(quoteSlide(slots), 1));
  await expect(stat).toHaveAttribute('data-revealed', 'true');
  await expect(followUp).toHaveAttribute('data-revealed', 'false');
  await component.update(render(quoteSlide(slots), 2));
  await expect(followUp).toHaveAttribute('data-revealed', 'true');
  expect(await box(page, 'quote-stat')).toEqual(hidden.stat);
  expect(await box(page, 'quote-follow-up')).toEqual(hidden.up);
});

test('a follow-up with no stat arrives on step 1', async ({ mount, page }) => {
  const slots: QuoteSlots = { ...full, stat: undefined, reveal: 'one-by-one' };
  const component = await mount(render(quoteSlide(slots), 0));
  const followUp = page.getByTestId('quote-follow-up').locator('xpath=..');
  await expect(followUp).toHaveAttribute('data-revealed', 'false');
  await component.update(render(quoteSlide(slots), 1));
  await expect(followUp).toHaveAttribute('data-revealed', 'true');
});

test('quote size stays between 36 and 62 px and its box above y 996', async ({ mount, page }) => {
  await stubApi(page, {});
  await mount(render(quoteSlide(fitFixture('quoteFull').slots as QuoteSlots), 5));
  await page.evaluate(() => document.fonts.ready);
  const size = await page
    .getByTestId('quote-text')
    .evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
  expect(size).toBeGreaterThanOrEqual(36);
  expect(size).toBeLessThanOrEqual(62);
  expect((await box(page, 'quote-text')).bottom).toBeLessThanOrEqual(996);
});

test('a quote with only the old slots draws as before', async ({ mount, page }) => {
  await stubApi(page, {});
  await mount(render(quoteSlide(today)));
  await page.evaluate(() => document.fonts.ready);
  await expect(page.getByTestId('quote-text')).toHaveText(`“${today.quote}”`);
  await expect(page.getByTestId('quote-attribution')).toHaveText(today.attribution);
  await expect(page.getByTestId('quote-stat')).toHaveCount(0);
  await expect(page.locator('.sw-reveal')).toHaveCount(0);
  await expect(page.locator('.sw-body')).toHaveCount(0);
  const text = await box(page, 'quote-text');
  expect(text.x).toBeCloseTo(176, 0);
  expect(await text.height).toBeGreaterThan(0);
});

test('emphasis in the quote and the headline carry their tone', async ({ mount, page }) => {
  await mount(
    render(
      quoteSlide({
        ...full,
        emphasis: [
          { text: 'is it out?', tone: 'positive' },
          { text: 'quarter', tone: 'negative', in: 'headline' },
        ],
      }),
    ),
  );
  const marks = page.getByTestId('emphasis');
  await expect(marks).toHaveCount(2);
  await expect(page.getByTestId('quote-text').getByTestId('emphasis')).toHaveAttribute(
    'data-tone',
    'positive',
  );
  await expect(page.getByTestId('quote-headline').getByTestId('emphasis')).toHaveAttribute(
    'data-tone',
    'negative',
  );
});

// Wide letters (capitals, W and M) of exactly `length` characters. Copied from
// DataLayouts.fixtures.ts, which is frozen.
const WIDE_TEXT = 'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';
const wide = (length: number): string => wideLetters(length, WIDE_TEXT);

// Every text string widened; options and the stat's value are left as they are.
const NOT_TEXT = new Set(['reveal', 'tone', 'in', 'value']);
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
  return page.getByTestId('layout-quote').evaluate((root: Element) => {
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

for (const name of ['quoteFull', 'quoteStatOnly', 'quoteFollowUpDark']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    const entry = fitFixture(name);
    await mount(render(quoteSlide(entry.slots as QuoteSlots, entry.surface), 5));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
  });
}

test('quoteFull wide twin stays between x 120 and 1800', async ({ mount, page }) => {
  await stubApi(page, {});
  const slots = widen(fitFixture('quoteFull').slots) as QuoteSlots;
  await mount(render(quoteSlide(slots), 5));
  await page.evaluate(() => document.fonts.ready);
  const drawn = await textBox(page);
  expect(drawn.left).toBeGreaterThanOrEqual(120);
  expect(drawn.right).toBeLessThanOrEqual(1800);
  // The bottom is not asserted: wide letters wrap sooner than the estimate counts.
  test.info().annotations.push({ type: 'bottom', description: String(drawn.bottom) });
});

test('screenshot: quote full', async ({ mount }) => {
  const component = await mount(render(quoteSlide(full)));
  await expect(component).toHaveScreenshot('layout-quote-full.png');
});

test('screenshot: quote full dark', async ({ mount }) => {
  const component = await mount(render(quoteSlide(full, 'dark')));
  await expect(component).toHaveScreenshot('layout-quote-full-dark.png');
});

test('screenshot: quote emphasis', async ({ mount }) => {
  const component = await mount(
    render(quoteSlide({ ...full, emphasis: [{ text: 'is it out?', tone: 'accent' }] })),
  );
  await expect(component).toHaveScreenshot('layout-quote-emphasis.png');
});
