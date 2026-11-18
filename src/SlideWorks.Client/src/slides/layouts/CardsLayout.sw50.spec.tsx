import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixturesOf } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { CardsSlots } from '../slots/cards';
import type { SlideDeck } from '../slots';

// The cards layout's SW-50 additions: the list fills the body's height, four
// cards lay out 2x2, large type for short cards, a plain fill, a pin, an icon,
// a callout, headline emphasis and a badge that takes two lines.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function cardsSlide(slots: CardsSlots, number = 3): RenderableSlide {
  return { number, layout: 'cards', part: 1, deepDive: false, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const SHORT = { title: 'Short title', text: 'Short text.', chips: [] as string[] };
const LONG_TEXT = 'A longer text that wraps onto several lines of the card. '.repeat(2);
const REGULAR = { title: 'A regular card', text: LONG_TEXT, chips: ['one'] };

const four = (card: CardsSlots['cards'][number]): CardsSlots['cards'] => [card, card, card, card];

const box = (page: Page, testId: string, index = 0) =>
  page.getByTestId(testId).nth(index).boundingBox();

// Wide letters (capitals, W and M) of exactly `length` characters.
const WIDE_TEXT =
  'Many Women Who Wander Wide Meadows Mow Moss With Me When Warm, ' +
  'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE_TEXT;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

// Keys whose strings are not text: they stay as they are.
const NOT_TEXT = new Set(['tone', 'fill', 'icon', 'reveal', 'in', 'surface', 'emphasis']);

function widen(value: unknown, key?: string): unknown {
  if (typeof value === 'string')
    return key !== undefined && NOT_TEXT.has(key) ? value : wide(value.length);
  if (Array.isArray(value)) return value.map((item) => widen(item, key));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, widen(v, k)]));
  }
  return value;
}

// The drawn extents of every text run, and the largest overshoot of a card's
// text past its card's bottom, in canvas px from the slide's top left.
function measure(page: Page) {
  return page.getByTestId('layout-cards').evaluate((root: Element) => {
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
    let overshoot = -Infinity;
    let cardBottom = -Infinity;
    for (const card of root.querySelectorAll('[data-testid="cards-card"]')) {
      const limit = card.getBoundingClientRect().bottom;
      cardBottom = Math.max(cardBottom, limit - slide.top);
      const inner = document.createTreeWalker(card, NodeFilter.SHOW_TEXT);
      for (let node = inner.nextNode(); node !== null; node = inner.nextNode()) {
        const range = document.createRange();
        range.selectNodeContents(node);
        for (const rect of range.getClientRects()) {
          if (rect.width > 0) overshoot = Math.max(overshoot, rect.bottom - limit);
        }
      }
    }
    return { left, right, bottom, overshoot, cardBottom };
  });
}

test('four cards are 2x2', async ({ mount, page }) => {
  await mount(render(cardsSlide({ headline: 'Four', cards: four(REGULAR) })));
  const boxes = await Promise.all([0, 1, 2, 3].map((i) => box(page, 'cards-card', i)));
  expect(new Set(boxes.map((b) => Math.round(b!.y))).size).toBe(2);
  expect(new Set(boxes.map((b) => Math.round(b!.x))).size).toBe(2);
  expect(boxes[0]!.width).toBeCloseTo(824, 0);
  expect(boxes[2]!.y - (boxes[0]!.y + boxes[0]!.height)).toBeCloseTo(28, 0);
  expect(boxes[1]!.x - (boxes[0]!.x + boxes[0]!.width)).toBeCloseTo(32, 0);
});

test('three cards stay in a row of 539 px columns', async ({ mount, page }) => {
  await mount(render(cardsSlide({ headline: 'Three', cards: [REGULAR, REGULAR, REGULAR] })));
  const boxes = await Promise.all([0, 1, 2].map((i) => box(page, 'cards-card', i)));
  expect(new Set(boxes.map((b) => Math.round(b!.y))).size).toBe(1);
  expect(boxes[0]!.width).toBeCloseTo(539, 0);
});

test('short cards use the large scale', async ({ mount, page }) => {
  const component = await mount(
    render(cardsSlide({ headline: 'Short', cards: [SHORT, SHORT, SHORT] })),
  );
  const list = page.getByTestId('cards-card').first().locator('xpath=../..');
  await expect(list).toHaveAttribute('data-scale', 'large');
  const title = page.getByTestId('cards-title').first();
  expect(await title.evaluate((e) => getComputedStyle(e).fontSize)).toBe('50px');
  expect(await title.evaluate((e) => getComputedStyle(e).fontWeight)).toBe('800');
  expect(
    await page
      .getByTestId('cards-text')
      .first()
      .evaluate((e) => getComputedStyle(e).fontSize),
  ).toBe('30px');
  await component.update(render(cardsSlide({ headline: 'Four', cards: four(SHORT) })));
  expect(await title.evaluate((e) => getComputedStyle(e).fontSize)).toBe('44px');
  await component.update(render(cardsSlide({ headline: 'Regular', cards: [REGULAR] })));
  await expect(list).toHaveAttribute('data-scale', 'regular');
  expect(await title.evaluate((e) => getComputedStyle(e).fontSize)).toBe('34px');
});

test("every card's bottom edge is the body's", async ({ mount, page }) => {
  for (const cards of [[SHORT, REGULAR, SHORT], four(REGULAR)]) {
    const component = await mount(render(cardsSlide({ headline: 'Fill', cards })));
    const body = (await box(page, 'cards-body'))!;
    const count = cards.length;
    const last = (await box(page, 'cards-card', count - 1))!;
    expect(last.y + last.height).toBeCloseTo(body.y + body.height, 0);
    expect(body.y + body.height).toBeLessThanOrEqual(996);
    if (count === 3) {
      const heights = await Promise.all([0, 1, 2].map((i) => box(page, 'cards-card', i)));
      for (const h of heights) expect(h!.height).toBeCloseTo(body.height, 0);
    }
    await component.unmount();
  }
});

test('highlighted card shows its pin', async ({ mount, page }) => {
  await mount(
    render(
      cardsSlide({
        headline: 'Pin',
        cards: [{ ...REGULAR, pin: 'Hold on to this' }, REGULAR, REGULAR],
      }),
    ),
  );
  await expect(page.getByTestId('pin')).toHaveCount(1);
  await expect(page.getByTestId('pin')).toHaveText('Hold on to this');
  const card = (await box(page, 'cards-card', 0))!;
  const pin = (await box(page, 'pin'))!;
  expect(card.x + card.width - (pin.x + pin.width)).toBeCloseTo(28, 0);
  expect(card.y + card.height - (pin.y + pin.height)).toBeCloseTo(28, 0);
  const text = (await box(page, 'cards-text'))!;
  expect(text.y + text.height).toBeLessThanOrEqual(pin.y);
});

test('plain fill draws the tone on the top edge', async ({ mount, page }) => {
  await mount(
    render(
      cardsSlide({
        headline: 'Plain',
        cards: [{ ...REGULAR, tone: 'positive', fill: 'plain' }, REGULAR, REGULAR],
      }),
    ),
  );
  const card = page.getByTestId('cards-card').first();
  const style = await card.evaluate((e) => {
    const css = getComputedStyle(e);
    const probe = document.createElement('span');
    probe.style.color = 'var(--sw-good)';
    e.appendChild(probe);
    const rule = getComputedStyle(probe).color;
    probe.remove();
    return {
      width: css.borderTopWidth,
      colour: css.borderTopColor,
      rule,
      background: css.backgroundColor,
    };
  });
  expect(style.width).toBe('8px');
  expect(style.colour).toBe(style.rule);
  const plain = await page
    .getByTestId('cards-card')
    .nth(1)
    .evaluate((e) => getComputedStyle(e).backgroundColor);
  expect(style.background).toBe(plain);
});

test('reveal still hides cards in the 2x2', async ({ mount, page }) => {
  const slots: CardsSlots = { headline: 'Reveal', cards: four(REGULAR), reveal: 'one-by-one' };
  const component = await mount(render(cardsSlide(slots), 2));
  const items = page.getByTestId('layout-cards').locator('li');
  await expect(items.nth(1)).toHaveAttribute('data-revealed', 'true');
  await expect(items.nth(2)).toHaveAttribute('data-revealed', 'false');
  const hidden = (await box(page, 'cards-card', 3))!;
  await component.update(render(cardsSlide(slots), 4));
  await expect(items.nth(3)).toHaveAttribute('data-revealed', 'true');
  expect((await box(page, 'cards-card', 3))!).toEqual(hidden);
});

test('callout under the cards', async ({ mount, page }) => {
  const slots: CardsSlots = {
    headline: 'Callout',
    cards: [REGULAR, REGULAR, REGULAR],
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(cardsSlide(slots), 3));
  const callout = (await box(page, 'body-callout'))!;
  const card = (await box(page, 'cards-card', 0))!;
  expect(card.y + card.height).toBeLessThanOrEqual(callout.y);
  expect(callout.y + callout.height).toBeLessThanOrEqual(996);
  const reveal = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(reveal).toHaveAttribute('data-revealed', 'false');
  await component.update(render(cardsSlide(slots), 4));
  await expect(reveal).toHaveAttribute('data-revealed', 'true');
  expect((await box(page, 'body-callout'))!).toEqual(callout);
});

test('icon sits above the badge', async ({ mount, page }) => {
  await mount(
    render(
      cardsSlide({
        headline: 'Icons',
        cards: [
          { ...SHORT, icon: 'coins', badge: 'Badge' },
          { ...SHORT, icon: 'not-an-icon', badge: 'Badge' },
          { ...SHORT, badge: 'Badge' },
        ],
      }),
    ),
  );
  await expect(page.getByTestId('block-icon')).toHaveCount(1);
  const icon = (await box(page, 'block-icon'))!;
  expect(icon.height).toBeCloseTo(44, 0);
  const badge = (await box(page, 'block-badge', 0))!;
  expect(icon.y + icon.height).toBeLessThanOrEqual(badge.y);
  // An unknown name draws no icon row: that card's badge sits as high as the plain one's.
  const second = (await box(page, 'block-badge', 1))!;
  const third = (await box(page, 'block-badge', 2))!;
  expect(second.y).toBeCloseTo(third.y, 0);
  expect(badge.y - second.y).toBeCloseTo(78, 0);
});

test('a long badge takes two lines', async ({ mount, page }) => {
  await mount(
    render(
      cardsSlide({
        headline: 'Badges',
        cards: [{ ...REGULAR, badge: 'PM' }, { ...REGULAR, badge: wide(36) }, REGULAR],
      }),
    ),
  );
  const one = (await box(page, 'block-badge', 0))!;
  const two = (await box(page, 'block-badge', 1))!;
  expect(one.height).toBe(30);
  expect(two.height).toBe(50);
  const first = (await box(page, 'cards-title', 0))!;
  const second = (await box(page, 'cards-title', 1))!;
  expect(second.y - first.y).toBeCloseTo(20, 0);
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      cardsSlide({
        headline: 'Everyone changes one habit',
        cards: [SHORT],
        emphasis: [{ text: 'one habit', tone: 'positive', in: 'headline' }],
      }),
    ),
  );
  const phrase = page.getByRole('heading', { level: 2 }).getByTestId('emphasis');
  await expect(phrase).toHaveText('one habit');
  await expect(phrase).toHaveAttribute('data-tone', 'positive');
});

for (const [name, fixture] of Object.entries(fitFixturesOf('cards'))) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = fixture.slots as CardsSlots;
    await mount(render(cardsSlide(slots), 5));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await measure(page);
    console.log(
      `${name}: drawn bottom ${drawn.bottom}, card bottom ${drawn.cardBottom}, overshoot ${drawn.overshoot}`,
    );
    expect(drawn.left, name).toBeGreaterThanOrEqual(120);
    expect(drawn.right, name).toBeLessThanOrEqual(1800);
    expect(drawn.bottom, name).toBeLessThanOrEqual(996);
    expect(drawn.overshoot, name).toBeLessThanOrEqual(0.5);
  });

  if (fixture.refusedBy !== 'fit') continue;
  test(`${name} with wide letters stays inside x 120-1800`, async ({ mount, page }) => {
    await stubApi(page, {});
    const slots = widen(fixture.slots) as CardsSlots;
    await mount(render(cardsSlide(slots), 5));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await measure(page);
    console.log(
      `${name} wide: drawn bottom ${drawn.bottom}, card bottom ${drawn.cardBottom}, overshoot ${drawn.overshoot}`,
    );
    expect(drawn.left, name).toBeGreaterThanOrEqual(120);
    expect(drawn.right, name).toBeLessThanOrEqual(1800);
    expect(drawn.bottom, name).toBeLessThanOrEqual(996);
  });
}

test('screenshot: cards 2×2', async ({ mount }) => {
  const component = await mount(
    render(
      cardsSlide({
        kicker: 'Make it stick',
        headline: 'Four habits a team can change this month',
        cards: [
          {
            badge: 'Plan',
            title: 'Write the demo sentence',
            text: LONG_TEXT,
            chips: ['story map'],
          },
          { badge: 'Build', title: 'Merge daily', text: LONG_TEXT, chips: ['trunk', 'flags'] },
          { badge: 'Ship', title: 'Release weekly', text: LONG_TEXT, chips: [] },
          { badge: 'Learn', title: 'Ask a customer', text: LONG_TEXT, chips: ['call'] },
        ],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-cards-2x2.png');
});

test('screenshot: cards pinned', async ({ mount }) => {
  const component = await mount(
    render(
      cardsSlide({
        headline: 'Three things worth keeping',
        cards: [
          {
            icon: 'coins',
            badge: 'Cost',
            title: 'Small batches',
            text: 'Cheaper to fix.',
            chips: [],
          },
          {
            icon: 'coins',
            badge: 'The team ships a small slice of work',
            title: 'Weekly ship',
            text: 'Feedback in days.',
            chips: [],
            tone: 'accent',
            fill: 'solid',
            pin: 'Hold on to this',
          },
          {
            icon: 'coins',
            badge: 'Risk',
            title: 'Early signal',
            text: 'Fail while cheap.',
            chips: [],
          },
        ],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-cards-pinned.png');
});

test('screenshot: cards plain', async ({ mount }) => {
  const component = await mount(
    render(
      cardsSlide({
        headline: 'What each slice gives back',
        cards: [
          { ...SHORT, tone: 'positive', fill: 'plain' },
          { ...SHORT, tone: 'negative', fill: 'plain' },
          { ...SHORT, tone: 'accent', fill: 'plain' },
        ],
        callout: { tone: 'info', text: 'Cut the batch, not the scope.' },
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-cards-plain.png');
});
