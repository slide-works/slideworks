import { test, expect } from '@playwright/experimental-ct-react';
import type { RenderableSlide } from '../SlideRenderer';
import { CARD_FIT_FIXTURES, cardFitFixture } from './CardLayouts.fixtures';
import { ACCENT, measure, render, sample } from './CardLayouts.helpers';

// The rules layout, rendered through SlideRenderer inside the real frame. Its
// screenshot tests stay in CardLayouts.spec, beside their baselines.

test.use({ viewport: { width: 1920, height: 1080 } });

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
// The shared max-content fixtures (src/contracts/fit-fixtures): prose and
// Title Case text at the most the validator accepts, at the layout's maximum
// count. Everything drawn stays inside x 120-1800 and y 150-996, so the last
// rule clears the footer text at y = 1023, and within LayoutRules' estimate
// below the body's top.
for (const [name, fixture] of Object.entries(CARD_FIT_FIXTURES)) {
  if (fixture.layout !== 'rules' || fixture.accepted === false) continue;
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
