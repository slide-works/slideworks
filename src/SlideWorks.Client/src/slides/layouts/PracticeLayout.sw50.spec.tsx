import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { PracticeSlots } from '../slots/practice';
import type { SlideDeck } from '../slots';

// The practice layout's SW-50 additions: emphasis in the headline, and a
// callout that goes through the shared block and can arrive on the last step,
// looking as it always has.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

// In a real part, so the tracker draws as it would in a deck.
function slideOf(slots: PracticeSlots): RenderableSlide {
  return { number: 3, layout: 'practice', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const box = (page: Page, testId: string) => page.getByTestId(testId).boundingBox();

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-practice').evaluate((root: Element) => {
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

const base: PracticeSlots = {
  headline: 'Slice by what a customer can do, not by layer',
  do: [{ lead: 'Start from one user action', text: 'and build every layer it needs, thinly.' }],
  comes_back: ['Feedback in days, not quarters.'],
  avoid: ['"API first, UI later."'],
};

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(render(slideOf({ ...base, emphasis: [{ text: 'not by layer', tone: 'negative' }] })));
  const phrase = page.getByTestId('practice-headline').getByTestId('emphasis');
  await expect(phrase).toHaveText('not by layer');
  await expect(phrase).toHaveAttribute('data-tone', 'negative');
});

test('practice callout arrives last and looks as before', async ({ mount, page }) => {
  const slots: PracticeSlots = {
    ...base,
    callout: {
      tone: 'bad',
      text: "If a slice can't be demoed, it isn't a slice yet.",
      reveal: 'last',
    },
  };
  const component = await mount(render(slideOf(slots), 0));
  const reveal = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(reveal).toHaveAttribute('data-revealed', 'false');
  await component.update(render(slideOf(slots), 1));
  await expect(reveal).toHaveAttribute('data-revealed', 'true');
  // It sits in the right column, 36 px under the Avoid list, as it did before it went through the shared block.
  const callout = page.getByTestId('block-callout');
  await expect(callout).toHaveAttribute('data-tone', 'bad');
  const avoid = (await box(page, 'practice-avoid'))!;
  const drawn = (await callout.boundingBox())!;
  expect(drawn.y - (avoid.y + avoid.height)).toBeCloseTo(36, 0);
  const right = (await box(page, 'practice-comes-back'))!;
  expect(drawn.x).toBeCloseTo(right.x, 0);
  expect(drawn.width).toBeCloseTo(right.width, 0);
});

test('a callout without reveal shows from the start', async ({ mount, page }) => {
  await mount(render(slideOf({ ...base, callout: { tone: 'info', text: 'Now' } })));
  await expect(page.getByTestId('block-callout')).toBeVisible();
  await expect(page.getByTestId('body-callout').locator('.sw-reveal')).toHaveCount(0);
});

// Capitals and wide letters of the same lengths.
const WIDE =
  'Many Women Who Wander Wide Meadows Mow Moss With Me When Warm, Whom We Name WOMBAT MUMMY ';
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

test('the practice fixture and a wide twin stay inside the box sideways', async ({
  mount,
  page,
}) => {
  await stubApi(page, {});
  const slots = fitFixture('practice').slots as PracticeSlots;
  const twin: PracticeSlots = {
    kicker: wide(slots.kicker!.length),
    headline: wide(slots.headline.length),
    do: slots.do.map((i) => ({ lead: wide(i.lead.length), text: wide(i.text.length) })),
    comes_back: slots.comes_back.map((t) => wide(t.length)),
    avoid: slots.avoid.map((t) => wide(t.length)),
    callout: { ...slots.callout!, text: wide(slots.callout!.text.length) },
  };
  for (const [name, drawn] of [
    ['practice', slots],
    ['practice wide', twin],
  ] as const) {
    const component = await mount(render(slideOf(drawn), 1));
    await page.evaluate(() => document.fonts.ready);
    const text = await textBox(page);
    expect(text.left, name).toBeGreaterThanOrEqual(120);
    expect(text.right, name).toBeLessThanOrEqual(1800);
    if (name === 'practice') expect(text.bottom, name).toBeLessThanOrEqual(996);
    await component.unmount();
  }
});
