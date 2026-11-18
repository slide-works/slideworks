import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { NumberedCardsSlots } from '../slots/numbered-cards';
import type { SlideDeck } from '../slots';

// The numbered-cards layout's SW-50 additions: two or three cards across three
// columns, cards that fill the height, the large scale, a highlighted card, a
// callout and emphasis in the headline.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function cardsSlide(slots: NumberedCardsSlots, number = 13): RenderableSlide {
  return { number, layout: 'numbered-cards', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const short = [
  { title: 'Who notices?', text: 'Name the customer.' },
  { title: 'What can they do?', text: 'One sentence.' },
  { title: 'What do they get?', text: 'A reason to stay.' },
];

const base: NumberedCardsSlots = {
  kicker: 'Before you split',
  headline: 'Three questions, in this order',
  cards: short,
};

const regular = [
  {
    title: 'Name the customer who will notice first',
    text: 'Write down the one person who would see this change on the day it ships, and why they would care about it.',
  },
  {
    title: 'Find the thinnest version',
    text: 'The smallest slice that still teaches the team something real about the customer and the work.',
  },
  ...short.slice(2),
];

const items = (page: Page) => page.getByTestId('layout-numbered-cards').locator('li');

test('two cards share three columns', async ({ mount, page }) => {
  await mount(render(cardsSlide({ ...base, cards: short.slice(0, 2) })));
  const boxes = await Promise.all([0, 1].map((i) => items(page).nth(i).boundingBox()));
  expect(Math.round(boxes[0]!.width)).toBe(541);
  expect(Math.round(boxes[1]!.width)).toBe(541);
  // The third column is empty: the second card ends at x = 120 + 541 + 28 + 541.
  expect(Math.abs(boxes[1]!.x + boxes[1]!.width - (120 + 541 * 2 + 28))).toBeLessThan(1);
  expect(boxes[1]!.x + boxes[1]!.width).toBeLessThan(1800 - 541);
});

test('three cards fill the three columns', async ({ mount, page }) => {
  await mount(render(cardsSlide(base)));
  const last = (await items(page).nth(2).boundingBox())!;
  expect(Math.round(last.width)).toBe(541);
  expect(last.x + last.width).toBeCloseTo(1800, 0);
});

test('four cards keep four columns', async ({ mount, page }) => {
  await mount(
    render(cardsSlide({ ...base, cards: [...short, { title: 'And then?', text: 'Ship it.' }] })),
  );
  for (let i = 0; i < 4; i++)
    expect(Math.round((await items(page).nth(i).boundingBox())!.width)).toBe(399);
});

test('short cards use the large scale', async ({ mount, page }) => {
  await mount(render(cardsSlide(base)));
  const list = page.getByTestId('layout-numbered-cards').locator('ol');
  await expect(list).toHaveAttribute('data-scale', 'large');
  await expect(page.getByTestId('numbered-cards-number').first()).toHaveCSS('font-size', '96px');
  await expect(page.getByTestId('numbered-cards-title').first()).toHaveCSS('font-size', '40px');
  await expect(page.getByTestId('numbered-cards-text').first()).toHaveCSS('font-size', '28px');
});

test('longer cards use the regular scale', async ({ mount, page }) => {
  await mount(render(cardsSlide({ ...base, cards: regular })));
  const list = page.getByTestId('layout-numbered-cards').locator('ol');
  await expect(list).toHaveAttribute('data-scale', 'regular');
  await expect(page.getByTestId('numbered-cards-number').first()).toHaveCSS('font-size', '64px');
  await expect(page.getByTestId('numbered-cards-title').first()).toHaveCSS('font-size', '30px');
  await expect(page.getByTestId('numbered-cards-text').first()).toHaveCSS('font-size', '23px');
});

test("every card's bottom edge is the body's", async ({ mount, page }) => {
  await mount(render(cardsSlide(base)));
  const body = (await page.getByTestId('numbered-cards-body').boundingBox())!;
  for (let i = 0; i < 3; i++) {
    const box = (await items(page).nth(i).boundingBox())!;
    expect(box.y + box.height).toBeCloseTo(body.y + body.height, 0);
    expect(box.y).toBeCloseTo(body.y, 0);
  }
});

test('a callout takes the height under the cards and the cards still end above it', async ({
  mount,
  page,
}) => {
  await mount(
    render(cardsSlide({ ...base, callout: { tone: 'info', text: 'Answer them in order.' } })),
  );
  const card = (await items(page).nth(0).boundingBox())!;
  const callout = (await page.getByTestId('body-callout').boundingBox())!;
  expect(callout.y).toBeGreaterThanOrEqual(card.y + card.height);
});

test('callout arrives after the last card', async ({ mount, page }) => {
  const slots: NumberedCardsSlots = {
    ...base,
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(cardsSlide(slots), 3));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  const before = (await page.getByTestId('body-callout').boundingBox())!;
  await component.update(render(cardsSlide(slots), 4));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
  expect(await page.getByTestId('body-callout').boundingBox()).toEqual(before);
});

test('a card without a callout leaves no callout markup', async ({ mount, page }) => {
  await mount(render(cardsSlide(base)));
  await expect(page.getByTestId('body-callout')).toHaveCount(0);
  await expect(page.getByTestId('pin')).toHaveCount(0);
  await expect(page.locator('[data-tone]')).toHaveCount(0);
});

test('a highlighted card shows its pin and tone', async ({ mount, page }) => {
  const cards = [
    { ...short[0]!, tone: 'positive' as const, pin: 'Hold on to this one' },
    { ...short[1]!, tone: 'accent' as const, fill: 'plain' as const },
    short[2]!,
  ];
  await mount(render(cardsSlide({ ...base, cards })));
  const card = (i: number) => page.getByTestId('numbered-cards-card').nth(i);
  await expect(page.getByTestId('pin')).toHaveCount(1);
  await expect(page.getByTestId('pin')).toHaveText('Hold on to this one');
  await expect(card(0)).toHaveAttribute('data-tone', 'positive');
  await expect(card(0)).toHaveCSS(
    'background-color',
    await page.evaluate(() => {
      const probe = document.createElement('i');
      probe.style.backgroundColor = 'var(--sw-good-soft)';
      document.body.append(probe);
      const colour = getComputedStyle(probe).backgroundColor;
      probe.remove();
      return colour;
    }),
  );
  // The pin sits at the bottom right, 28 px in.
  const box = (await card(0).boundingBox())!;
  const pin = (await page.getByTestId('pin').boundingBox())!;
  expect(box.x + box.width - (pin.x + pin.width)).toBeCloseTo(28, 0);
  expect(box.y + box.height - (pin.y + pin.height)).toBeCloseTo(28, 0);
  // fill 'plain': the card's own surface under an 8 px rule in the tone.
  await expect(card(1)).toHaveCSS('border-top-width', '8px');
  await expect(card(1)).toHaveCSS(
    'background-color',
    await card(2).evaluate((e) => getComputedStyle(e).backgroundColor),
  );
  await expect(card(2)).not.toHaveAttribute('data-tone');
});

test('a fill without a tone is ignored', async ({ mount, page }) => {
  await mount(
    render(cardsSlide({ ...base, cards: [{ ...short[0]!, fill: 'solid' }, ...short.slice(1)] })),
  );
  await expect(page.getByTestId('numbered-cards-card').first()).not.toHaveAttribute('data-fill');
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      cardsSlide({
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
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE_TEXT;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

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
  return page.getByTestId('layout-numbered-cards').evaluate((root: Element) => {
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

for (const name of ['numberedCardsFourMax', 'numberedCardsLargeThree', 'numberedCardsCallout']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = fitFixture(name).slots as NumberedCardsSlots;
    // Every step shown, so a callout that reveals last is drawn.
    await mount(render(cardsSlide(slots), 10));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    test.info().annotations.push({ type: 'bottom', description: `${drawn.bottom}` });
  });

  test(`${name} wide twin stays between x 120 and 1800`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = widen(fitFixture(name).slots) as NumberedCardsSlots;
    await mount(render(cardsSlide(slots), 10));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    // The bottom is not asserted: wide letters wrap sooner than the estimate counts.
    test.info().annotations.push({ type: 'bottom', description: `${drawn.bottom}` });
  });
}

test('screenshot: numbered-cards two', async ({ mount }) => {
  const component = await mount(
    render(
      cardsSlide({
        ...base,
        cards: [{ ...short[0]!, tone: 'accent', pin: 'Start here' }, short[1]!],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-numbered-cards-two.png');
});
