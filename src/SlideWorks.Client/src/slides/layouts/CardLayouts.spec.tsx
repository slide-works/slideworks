import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { SlideDeck } from '../slots';
import { CARD_FIT_FIXTURES, cardFitFixture } from './CardLayouts.fixtures';

// The numbered-cards, rules and cards layouts with the chip and badge blocks,
// rendered through SlideRenderer inside the real frame.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function sample(number: number): RenderableSlide {
  const slide = sampleDeck.slides.find((s) => s.number === number);
  if (slide === undefined) throw new Error(`sample deck has no slide ${number}`);
  return slide as RenderableSlide;
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const ACCENT = 'rgb(47, 91, 234)';
const ACCENT_DARK = 'rgb(30, 58, 138)';
const ACCENT_LIGHT = 'rgb(228, 234, 253)';

type Box = { left: number; top: number; right: number; bottom: number };

// What the layout draws, in canvas px from the slide's top left: every run of
// text and every painted box (a background, shadow or border) inside the
// layout, as ContentLayouts.spec measures it. Layout boxes that only stretch to
// fill the body, such as the body itself or a list, are left out. Also the top
// of every element box, the last list item's box, the body's box and the
// footer's top.
async function measure(page: Page, layoutTestId: string) {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId(layoutTestId).evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const rel = (rect: DOMRect): Box => ({
      left: rect.left - slide.left,
      top: rect.top - slide.top,
      right: rect.right - slide.left,
      bottom: rect.bottom - slide.top,
    });
    const drawn = { left: Infinity, top: Infinity, right: -Infinity, bottom: -Infinity };
    const add = (rect: DOMRect) => {
      if (rect.width === 0 && rect.height === 0) return;
      const box = rel(rect);
      drawn.left = Math.min(drawn.left, box.left);
      drawn.top = Math.min(drawn.top, box.top);
      drawn.right = Math.max(drawn.right, box.right);
      drawn.bottom = Math.max(drawn.bottom, box.bottom);
    };
    // A 20 px/1 kicker's glyphs rise a little above its line box; the boxes don't.
    let boxTop = Infinity;
    for (const element of root.querySelectorAll('*')) {
      const rect = element.getBoundingClientRect();
      if (rect.width > 0 || rect.height > 0) boxTop = Math.min(boxTop, rel(rect).top);
      const style = getComputedStyle(element);
      const painted =
        style.backgroundColor !== 'rgba(0, 0, 0, 0)' ||
        style.boxShadow !== 'none' ||
        parseFloat(style.borderTopWidth) > 0;
      if (painted) add(rect);
    }
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      // A rule's numeral is 150 px on a 0.85 line, so its text rect (the font's
      // whole ascent and descent, about 189 px) runs some 30 px past its line
      // box. Digits have no descenders, so its ink ends at the baseline: a
      // zero-height probe on the baseline measures that instead, and the
      // numeral's left and right come from its own box.
      if (node.parentElement?.matches('[data-testid="rules-number"]')) {
        const probe = document.createElement('span');
        probe.style.display = 'inline-block';
        probe.style.height = '0';
        node.parentElement.append(probe);
        const baseline = rel(probe.getBoundingClientRect()).bottom;
        probe.remove();
        const numeral = rel(node.parentElement.getBoundingClientRect());
        drawn.left = Math.min(drawn.left, numeral.left);
        drawn.right = Math.max(drawn.right, numeral.right);
        drawn.bottom = Math.max(drawn.bottom, baseline);
        continue;
      }
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) add(rect);
    }
    const items = root.querySelectorAll('li');
    const lastItem = rel(items[items.length - 1]!.getBoundingClientRect());
    const body = rel(root.querySelector('.sw-body')!.getBoundingClientRect());
    const footer = document.querySelector('[data-testid="slide-footer"]')!.getBoundingClientRect();
    return { drawn, boxTop, body, lastItem, footerTop: footer.top - slide.top };
  });
}

test('numbered-cards renders the sample slide', async ({ mount, page }) => {
  await mount(render(sample(13)));

  const layout = page.getByTestId('layout-numbered-cards');
  await expect(layout.getByTestId('numbered-cards-kicker')).toHaveText('Before you split a story');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'Four questions, in this order',
  );
  await expect(layout.getByRole('listitem')).toHaveCount(4);
  await expect(layout.getByTestId('numbered-cards-number')).toHaveText(['1', '2', '3', '4']);
  await expect(layout.getByTestId('numbered-cards-number').first()).toHaveCSS('color', ACCENT);
  await expect(layout.getByRole('heading', { level: 3 })).toHaveText([
    'Who notices?',
    "What's the thinnest version?",
    'Can it hide behind a flag?',
    'What would we learn?',
  ]);
  await expect(layout.getByTestId('numbered-cards-text').nth(3)).toHaveText(
    'If the answer is "nothing", pick another slice.',
  );
  // No reveal: every card shows at step 0, in one row from left to right.
  for (let i = 0; i < 4; i++) await expect(layout.getByRole('listitem').nth(i)).toBeVisible();
  const first = await layout.getByRole('listitem').nth(0).boundingBox();
  const last = await layout.getByRole('listitem').nth(3).boundingBox();
  expect(first!.y).toBe(last!.y);
  expect(first!.x).toBeLessThan(last!.x);
});

test('numbered cards one-by-one', async ({ mount, page }) => {
  const slide = sample(13);
  const oneByOne = {
    ...slide,
    slots: { ...(slide.slots as object), reveal: 'one-by-one' },
  } as RenderableSlide;
  const cards = page.getByTestId('layout-numbered-cards').locator('li');

  const none = await mount(render(oneByOne, 0));
  await expect(cards).toHaveCount(4);
  for (let i = 0; i < 4; i++) await expect(cards.nth(i)).toBeHidden();
  await none.unmount();

  const two = await mount(render(oneByOne, 2));
  await expect(cards.nth(0)).toBeVisible();
  await expect(cards.nth(1)).toBeVisible();
  await expect(cards.nth(2)).toBeHidden();
  await expect(cards.nth(3)).toBeHidden();
  // Hidden cards keep their space: visibility, never display:none.
  await expect(cards.nth(3)).toHaveCSS('visibility', 'hidden');
  expect((await cards.nth(3).boundingBox())?.height).toBeGreaterThan(0);
  await two.unmount();

  await mount(render(oneByOne, 4));
  for (let i = 0; i < 4; i++) await expect(cards.nth(i)).toBeVisible();
});

test('rules renders the sample slide with outline numerals', async ({ mount, page }) => {
  await mount(render(sample(14)));

  const layout = page.getByTestId('layout-rules');
  await expect(layout.getByTestId('rules-kicker')).toHaveText('Make it stick');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'Three rules for the first month',
  );
  await expect(layout.getByRole('listitem')).toHaveCount(3);
  await expect(layout.getByTestId('rules-number')).toHaveText(['1', '2', '3']);
  // Outline numerals: no fill, a 3 px accent stroke.
  const numeral = layout.getByTestId('rules-number').first();
  await expect(numeral).toHaveCSS('color', 'rgba(0, 0, 0, 0)');
  await expect(numeral).toHaveCSS('-webkit-text-stroke-width', '3px');
  await expect(numeral).toHaveCSS('-webkit-text-stroke-color', ACCENT);
  await expect(layout.getByRole('heading', { level: 3 })).toHaveText([
    'Merge to main every day',
    'Demo something every Friday',
    'Split any story older than a week',
  ]);
  await expect(layout.getByTestId('rules-text').nth(1)).toHaveText(
    "To a customer if you can, and to the team if you can't.",
  );
  for (let i = 0; i < 3; i++) await expect(layout.getByRole('listitem').nth(i)).toBeVisible();
});

test('rules one-by-one shows rule n at step n', async ({ mount, page }) => {
  const slide = sample(14);
  await mount(
    render(
      { ...slide, slots: { ...(slide.slots as object), reveal: 'one-by-one' } } as RenderableSlide,
      1,
    ),
  );
  const rules = page.getByTestId('layout-rules').locator('li');
  await expect(rules.nth(0)).toBeVisible();
  await expect(rules.nth(1)).toBeHidden();
  await expect(rules.nth(2)).toBeHidden();
});

test('cards show badge and chips', async ({ mount, page }) => {
  await mount(render(sample(15)));

  const layout = page.getByTestId('layout-cards');
  await expect(layout.getByTestId('cards-kicker')).toHaveText('Who does what');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText('Everyone changes one habit');
  const cards = layout.getByRole('listitem');
  await expect(cards).toHaveCount(3);

  await expect(layout.getByTestId('block-badge')).toHaveText(['Product', 'Engineering', 'Design']);
  // Badges carry no tone (SW-19 D5): every badge takes the accent.
  for (let i = 0; i < 3; i++) {
    const badge = layout.getByTestId('block-badge').nth(i);
    await expect(badge).toHaveCSS('background-color', ACCENT_LIGHT);
    await expect(badge).toHaveCSS('color', ACCENT_DARK);
    await expect(badge).toHaveCSS('text-transform', 'uppercase');
  }
  await expect(layout.getByRole('heading', { level: 3 })).toHaveText([
    'Write the demo sentence',
    'Merge daily, behind flags',
    'Design the thin version too',
  ]);
  await expect(cards.nth(0).getByTestId('cards-text')).toHaveText(
    'Every story starts with "Now a customer can …".',
  );
  await expect(cards.nth(0).getByTestId('block-chip')).toHaveText(['story map', 'flag plan']);
  await expect(cards.nth(1).getByTestId('block-chip')).toHaveText(['trunk', 'flags', 'pairing']);
  await expect(cards.nth(2).getByTestId('block-chip')).toHaveText(['release 1 mock']);
  await expect(cards.nth(2).getByTestId('block-chip')).toHaveCSS(
    'background-color',
    'rgb(238, 241, 246)',
  );
  // Each card's badge sits above its title.
  const badge = await cards.nth(0).getByTestId('block-badge').boundingBox();
  const title = await cards.nth(0).getByRole('heading', { level: 3 }).boundingBox();
  expect(badge!.y + badge!.height).toBeLessThanOrEqual(title!.y);
});

test('cards without a badge or chips leave both out', async ({ mount, page }) => {
  await mount(
    render({
      ...sample(15),
      slots: {
        headline: 'One card',
        cards: [{ title: 'Plain card', text: 'No badge and no chips.', chips: [] }],
      },
    } as RenderableSlide),
  );
  const layout = page.getByTestId('layout-cards');
  await expect(layout.getByRole('listitem')).toHaveCount(1);
  await expect(layout.getByTestId('cards-kicker')).toHaveCount(0);
  await expect(layout.getByTestId('block-badge')).toHaveCount(0);
  await expect(layout.getByTestId('cards-chips')).toHaveCount(0);
  await expect(layout.getByTestId('cards-title')).toHaveText('Plain card');
});

test('cards one-by-one shows card n at step n', async ({ mount, page }) => {
  const slide = sample(15);
  await mount(
    render(
      { ...slide, slots: { ...(slide.slots as object), reveal: 'one-by-one' } } as RenderableSlide,
      2,
    ),
  );
  const cards = page.getByTestId('layout-cards').locator('li');
  await expect(cards.nth(0)).toBeVisible();
  await expect(cards.nth(1)).toBeVisible();
  await expect(cards.nth(2)).toBeHidden();
});

// The shared max-content fixtures (src/contracts/fit-fixtures.json): prose,
// Title Case and wide-letter text at the most the validator accepts, at each
// layout's maximum count. Everything drawn stays inside x 120-1800 and
// y 150-996, so the last card or rule clears the footer text at y = 1023. A rules fixture also stays within LayoutRules' estimate
// below the body's top.
for (const [name, fixture] of Object.entries(CARD_FIT_FIXTURES)) {
  if (fixture.accepted === false) continue;
  test(`max-length text stays inside 1920x1080: ${name}`, async ({ mount, page }) => {
    const { layout, slots, estimate } = fixture;
    await mount(render({ ...sample(13), layout, slots } as RenderableSlide, 4));

    const { drawn, boxTop, body, lastItem, footerTop } = await measure(page, `layout-${layout}`);
    console.log(
      `${name}: body top ${body.top}, last item bottom ${lastItem.bottom}, drawn bottom ${drawn.bottom}` +
        (estimate === undefined ? '' : `, estimate bottom ${body.top + estimate}`),
    );
    // The headline takes two lines (SlideHeading.spec), so the body starts at
    // y = 150 + 42 + 2 x 69.12 + 46.
    expect(body.top).toBeCloseTo(376.2, 0);
    expect(footerTop).toBeGreaterThanOrEqual(1023);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(boxTop).toBeGreaterThanOrEqual(150);
    expect(lastItem.bottom).toBeLessThanOrEqual(996);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    if (estimate !== undefined) expect(drawn.bottom).toBeLessThanOrEqual(body.top + estimate + 1);
  });
}

// Four rules of wide-letter text at the character limits estimate two lines of
// text each, 634.8 px against the 620 px body (LayoutRules.Rules, SW-124), and
// they draw past y = 996. The validator refuses them (FitFixturesTests), so
// they are not a maximum to hold to 996; this checks the shared fixture still
// says so and that the text stays inside the sides.
test('max-length wide-letter rules are refused by the validator', async ({ mount, page }) => {
  const fixture = cardFitFixture('rulesWide');
  expect(fixture.layout).toBe('rules');
  expect(fixture.accepted).toBe(false);
  expect(fixture.refusedBy).toBe('fit');
  expect(fixture.estimate).toBeGreaterThan(620);

  await mount(render({ ...sample(14), slots: fixture.slots } as RenderableSlide, 4));
  const { drawn, lastItem } = await measure(page, 'layout-rules');
  console.log(`rulesWide: last item bottom ${lastItem.bottom}, drawn bottom ${drawn.bottom}`);
  expect(drawn.left).toBeGreaterThanOrEqual(120);
  expect(drawn.right).toBeLessThanOrEqual(1800);
});

test('screenshot: numbered-cards', async ({ mount }) => {
  const component = await mount(render(sample(13)));
  await expect(component).toHaveScreenshot('layout-numbered-cards.png');
});

test('screenshot: rules', async ({ mount }) => {
  const component = await mount(render(sample(14)));
  await expect(component).toHaveScreenshot('layout-rules.png');
});

// The rules layout at its maximum: four rules of the longest titles and text.
test('screenshot: rules at max length', async ({ mount }) => {
  const component = await mount(
    render({ ...sample(14), slots: cardFitFixture('rulesProse').slots } as RenderableSlide, 4),
  );
  await expect(component).toHaveScreenshot('layout-rules-max.png');
});

test('screenshot: cards', async ({ mount }) => {
  const component = await mount(render(sample(15)));
  await expect(component).toHaveScreenshot('layout-cards.png');
});
