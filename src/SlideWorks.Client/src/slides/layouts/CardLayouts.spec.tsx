import { test, expect } from '@playwright/experimental-ct-react';
import type { RenderableSlide } from '../SlideRenderer';
import { CARD_FIT_FIXTURES, cardFitFixture } from './CardLayouts.fixtures';
import { ACCENT_DARK, ACCENT_LIGHT, measure, render, sample } from './CardLayouts.helpers';

// The cards layout with the chip and badge blocks, rendered through
// SlideRenderer inside the real frame, and the screenshots of all three card
// layouts: a baseline lives in a folder named after this file, so the
// numbered-cards and rules screenshots stay here. Their other tests are in
// NumberedCardsLayout.spec and RulesLayout.spec.

test.use({ viewport: { width: 1920, height: 1080 } });

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
// The shared max-content fixtures (src/contracts/fit-fixtures): prose, Title
// Case and wide-letter text at the most the validator accepts, at the layout's
// maximum count. Everything drawn stays inside x 120-1800 and y 150-996, so
// the last card clears the footer text at y = 1023.
for (const [name, fixture] of Object.entries(CARD_FIT_FIXTURES)) {
  if (fixture.layout !== 'cards' || fixture.accepted === false) continue;
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
