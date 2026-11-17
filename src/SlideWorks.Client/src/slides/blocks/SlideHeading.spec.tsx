import { test, expect } from '@playwright/experimental-ct-react';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { SlideFrame } from '../SlideFrame';
import { cardFitFixture } from '../layouts/CardLayouts.fixtures';
import { SlideHeading } from './SlideHeading';

// The shared kicker and headline, inside the real frame with a body under it.

test.use({ viewport: { width: 1920, height: 1080 } });

const TWO_LINES = 'Every slip we had last year started as a big batch that nobody could ship';

function slide(kicker: string | undefined, headline: string) {
  return (
    <SlideFrame
      layout="rules"
      dark={false}
      part={1}
      parts={['One', 'Two']}
      number={2}
      total={9}
      deckTitle="Deck"
      author="Sam Rivera"
      look={lookViewOf('talk', 'cobalt')}
      deepDive={false}
      titleStamp={null}
    >
      <div data-testid="layout-rules" className="sw-layout">
        <SlideHeading layout="rules" kicker={kicker} headline={headline} />
        <div data-testid="body" className="sw-body" />
      </div>
    </SlideFrame>
  );
}

async function top(page: import('playwright/test').Page, testId: string) {
  const frame = await page.getByTestId('slide-frame').boundingBox();
  const box = await page.getByTestId(testId).boundingBox();
  return { top: box!.y - frame!.y, height: box!.height };
}

test('the body starts at y = 376 under a kicker and a two-line headline', async ({
  mount,
  page,
}) => {
  await mount(slide('The pattern', TWO_LINES));
  await page.evaluate(() => document.fonts.ready);

  await expect(page.getByTestId('rules-kicker')).toHaveText('The pattern');
  await expect(page.getByRole('heading', { level: 2 })).toHaveText(TWO_LINES);
  const kicker = await top(page, 'rules-kicker');
  const headline = await top(page, 'rules-headline');
  const body = await top(page, 'body');
  expect(kicker.top).toBe(150);
  expect(kicker.height).toBe(20);
  expect(headline.top).toBe(192);
  // Two 64 px lines at a 1.08 line height: 138.24 px.
  expect(headline.height).toBeCloseTo(2 * 64 * 1.08, 0);
  expect(body.top).toBeCloseTo(376, 0);
});

test('without a kicker the headline starts at the content box top', async ({ mount, page }) => {
  await mount(slide(undefined, 'One line'));
  await expect(page.getByTestId('rules-kicker')).toHaveCount(0);
  const headline = await top(page, 'rules-headline');
  expect(headline.top).toBe(150);
  expect(headline.height).toBeCloseTo(64 * 1.08, 0);
  expect((await top(page, 'body')).top).toBeCloseTo(150 + 69.12 + 46, 0);
});

// The longest headlines the limits accept (80 characters, the max fixtures'),
// in Title Case and in wide letters (capitals and M and W). SlideFit budgets
// every content body from y = 376, under a kicker and at most two headline
// lines, so each must still take exactly two.
for (const [kind, name] of [
  ['title case', 'numberedCardsTitleCase'],
  ['wide', 'numberedCardsWide'],
] as const) {
  test(`an 80-character ${kind} headline takes two lines and the body starts at y = 376`, async ({
    mount,
    page,
  }) => {
    const { kicker, headline: text } = cardFitFixture(name).slots;
    expect(text).toHaveLength(80);
    await mount(slide(kicker, text));
    await page.evaluate(() => document.fonts.ready);

    await expect(page.getByRole('heading', { level: 2 })).toHaveText(text);
    const headline = await top(page, 'rules-headline');
    expect(headline.top).toBe(192);
    expect(headline.height).toBeCloseTo(2 * 64 * 1.08, 0);
    expect((await top(page, 'body')).top).toBeCloseTo(376, 0);
  });
}
