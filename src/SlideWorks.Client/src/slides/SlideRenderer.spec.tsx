import { test, expect } from '@playwright/experimental-ct-react';
import { LAYOUT_SLOTS } from '../test-support/fixtures/layoutSlots';
import { stubApi } from '../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from './SlideRenderer';
import { LAYOUT_NAMES } from './slots';

const deck = {
  title: 'Ship smaller',
  author: 'Sam Rivera',
  parts: ['The cost', 'Slice it', 'Ship it', 'Make it stick'],
};

test('renders every layout in the registry at 1920×1080', async ({ mount, page }) => {
  await stubApi(page, {});
  await mount(
    <div>
      {LAYOUT_NAMES.map((layout, index) => (
        <SlideRenderer
          key={layout}
          slide={
            { number: index + 1, layout, part: 1, slots: LAYOUT_SLOTS[layout] } as RenderableSlide
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
