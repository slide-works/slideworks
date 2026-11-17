import { test, expect } from '@playwright/experimental-ct-react';
import { LAYOUT_SLOTS } from '../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../test-support/fixtures/lookViewOf';
import { stubApi } from '../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from './SlideRenderer';
import { LAYOUT_NAMES } from './slots';

const deck = {
  title: 'Ship smaller',
  author: 'Sam Rivera',
  parts: ['The cost', 'Slice it', 'Ship it', 'Make it stick'],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

test('renders every layout in the registry at 1920×1080', async ({ mount, page }) => {
  await stubApi(page, {});
  await mount(
    <div>
      {LAYOUT_NAMES.map((layout, index) => (
        <SlideRenderer
          key={layout}
          slide={
            {
              number: index + 1,
              layout,
              part: 1,
              deepDive: false,
              slots: LAYOUT_SLOTS[layout],
            } as RenderableSlide
          }
          deck={deck}
          total={LAYOUT_NAMES.length}
          step={0}
        />
      ))}
    </div>,
  );

  for (const [index, layout] of LAYOUT_NAMES.entries()) {
    const slide = page.getByTestId(`slide-${index + 1}`);
    await expect(slide.getByTestId(`layout-${layout}`)).toBeAttached();
    expect(await slide.boundingBox()).toMatchObject({ width: 1920, height: 1080 });
  }
});

// The look goes on the slide's root, so a palette re-skins light and dark slides alike.
test('the same slides under Cobalt and Ember compute different colours', async ({
  mount,
  page,
}) => {
  await stubApi(page, {});
  const under = (palette: string, layout: 'bullets' | 'title', number: number) => (
    <SlideRenderer
      slide={
        { number, layout, part: 1, deepDive: false, slots: LAYOUT_SLOTS[layout] } as RenderableSlide
      }
      deck={{ ...deck, look: lookViewOf('talk', palette) }}
      total={4}
      step={0}
    />
  );
  await mount(
    <div>
      {under('cobalt', 'bullets', 1)}
      {under('ember', 'bullets', 2)}
      {under('cobalt', 'title', 3)}
      {under('ember', 'title', 4)}
    </div>,
  );

  const accentOf = (n: number) =>
    page
      .getByTestId(`slide-${n}`)
      .evaluate((el) => getComputedStyle(el).getPropertyValue('--sw-accent').trim());
  const cobaltAccent = await accentOf(1);
  const emberAccent = await accentOf(2);
  expect(cobaltAccent).toBe(lookViewOf('talk', 'cobalt').palette.tokens.accent);
  expect(emberAccent).toBe(lookViewOf('talk', 'ember').palette.tokens.accent);
  expect(emberAccent).not.toBe(cobaltAccent);

  const backgroundOf = (n: number) =>
    page
      .getByTestId(`slide-${n}`)
      .getByTestId('slide-frame')
      .evaluate((el) => {
        const style = getComputedStyle(el);
        return `${style.backgroundColor} ${style.backgroundImage}`;
      });
  await expect(page.getByTestId('slide-3').getByTestId('slide-frame')).toHaveAttribute(
    'data-dark',
    'true',
  );
  expect(await backgroundOf(4)).not.toBe(await backgroundOf(3));
});
