import { test, expect } from '@playwright/experimental-ct-react';
import type { RenderableSlide } from '../SlideRenderer';
import { CARD_FIT_FIXTURES } from './CardLayouts.fixtures';
import { deck, measure, render, rgbOf, sample } from './CardLayouts.helpers';

// The numbered-cards layout, rendered through SlideRenderer inside the real
// frame. Its screenshot test stays in CardLayouts.spec, beside its baseline.

test.use({ viewport: { width: 1920, height: 1080 } });

test('numbered-cards renders the sample slide', async ({ mount, page }) => {
  await mount(render(sample(13)));

  const layout = page.getByTestId('layout-numbered-cards');
  await expect(layout.getByTestId('numbered-cards-kicker')).toHaveText('Before you split a story');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'Four questions, in this order',
  );
  await expect(layout.getByRole('listitem')).toHaveCount(4);
  await expect(layout.getByTestId('numbered-cards-number')).toHaveText(['1', '2', '3', '4']);
  // The sample slide is a deep dive, so its numerals are in the deep-dive colour.
  await expect(layout.getByTestId('numbered-cards-number').first()).toHaveCSS(
    'color',
    rgbOf(deck.look.palette.tokens.dd),
  );
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

test('a deep dive draws the kicker and the numerals in the deep-dive colour', async ({
  mount,
  page,
}) => {
  const { tokens } = deck.look.palette;
  // The sample slide is a deep dive; the same slide without the flag first.
  const component = await mount(render({ ...sample(13), deepDive: false }));
  const layout = page.getByTestId('layout-numbered-cards');
  await expect(layout.getByTestId('numbered-cards-kicker')).toHaveCSS(
    'color',
    rgbOf(tokens.accent),
  );
  await expect(layout.getByTestId('numbered-cards-number').first()).toHaveCSS(
    'color',
    rgbOf(tokens.accent),
  );

  await component.update(render(sample(13)));
  await expect(page.getByTestId('slide-frame')).toHaveAttribute('data-deep-dive', 'true');
  await expect(layout.getByTestId('numbered-cards-kicker')).toHaveCSS('color', rgbOf(tokens.dd));
  for (let i = 0; i < 4; i++) {
    await expect(layout.getByTestId('numbered-cards-number').nth(i)).toHaveCSS(
      'color',
      rgbOf(tokens.dd),
    );
  }
  // The footer numeral and the progress bar keep the accent.
  await expect(page.getByTestId('slide-footer-text').locator('b')).toHaveCSS(
    'color',
    rgbOf(tokens['accent-dark']),
  );
  await expect(page.getByTestId('slide-progress')).toHaveCSS(
    'background-color',
    rgbOf(tokens.accent),
  );
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
// The shared max-content fixtures (src/contracts/fit-fixtures): prose, Title
// Case and wide-letter text at the most the validator accepts, at the layout's
// maximum count. Everything drawn stays inside x 120-1800 and y 150-996, so
// the last card clears the footer text at y = 1023.
for (const [name, fixture] of Object.entries(CARD_FIT_FIXTURES)) {
  if (fixture.layout !== 'numbered-cards' || fixture.accepted === false) continue;
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
