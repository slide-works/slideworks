import { test, expect } from '@playwright/experimental-ct-react';
import { SlideFrame, type SlideFrameProps } from './SlideFrame';

test.use({ viewport: { width: 1920, height: 1080 } });

const deck = {
  parts: ['The cost', 'Slice it', 'Ship it', 'Make it stick'],
  deckTitle: 'Ship smaller',
  author: 'Sam Rivera',
};

// A plain function, not a component: CT can only mount components from source files.
function slide(props: Partial<Omit<SlideFrameProps, 'children'>> = {}) {
  return (
    <SlideFrame layout="bullets" dark={false} part={2} number={3} total={16} {...deck} {...props}>
      <h2>Every slip we had last year started as a big batch</h2>
    </SlideFrame>
  );
}

test('content slide shows tracker with current part and footer', async ({ mount, page }) => {
  await mount(slide());

  const frame = page.getByTestId('slide-frame');
  await expect(frame).toHaveAttribute('data-dark', 'false');
  await expect(frame).toHaveAttribute('data-layout', 'bullets');

  const parts = page.getByTestId('part-tracker-part');
  await expect(parts).toHaveText(deck.parts);
  await expect(parts.nth(0)).toHaveAttribute('data-state', 'past');
  await expect(parts.nth(1)).toHaveAttribute('data-state', 'on');
  await expect(parts.nth(1)).toHaveAttribute('aria-current', 'step');
  await expect(parts.nth(2)).toHaveAttribute('data-state', 'next');
  await expect(parts.nth(3)).toHaveAttribute('data-state', 'next');
  await expect(page.locator('[aria-current]')).toHaveCount(1);

  await expect(page.getByTestId('slide-footer')).toHaveText('3 / 16 · Ship smaller · Sam Rivera');
});

test('title slide is dark without tracker or footer', async ({ mount, page }) => {
  await mount(slide({ layout: 'title', dark: true, part: null, number: 1 }));

  const frame = page.getByTestId('slide-frame');
  await expect(frame).toHaveAttribute('data-dark', 'true');
  await expect(frame).toHaveCSS('color', 'rgb(255, 255, 255)');
  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
  await expect(page.getByTestId('slide-footer')).toHaveCount(0);
  await expect(page.getByTestId('slide-progress')).toHaveCount(1);
});

test('divider and close slides are dark without tracker or footer', async ({ mount, page }) => {
  for (const layout of ['divider', 'close'] as const) {
    const component = await mount(slide({ layout, dark: true, part: 1 }));
    await expect(page.getByTestId('slide-frame')).toHaveAttribute('data-dark', 'true');
    await expect(page.getByTestId('part-tracker')).toHaveCount(0);
    await expect(page.getByTestId('slide-footer')).toHaveCount(0);
    await component.unmount();
  }
});

test('deck without parts shows no tracker', async ({ mount, page }) => {
  await mount(slide({ parts: [], part: null }));

  await expect(page.getByTestId('slide-frame')).toBeAttached();
  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
  await expect(page.getByTestId('slide-footer')).toHaveText('3 / 16 · Ship smaller · Sam Rivera');
});

test('slide outside any part highlights no part', async ({ mount, page }) => {
  await mount(slide({ part: null }));

  await expect(page.getByTestId('part-tracker-part')).toHaveCount(4);
  await expect(page.locator('[data-testid="part-tracker-part"][data-state="next"]')).toHaveCount(4);
  await expect(page.locator('[aria-current]')).toHaveCount(0);
});

test('footer leaves out a missing author', async ({ mount, page }) => {
  await mount(slide({ author: null }));

  await expect(page.getByTestId('slide-footer')).toHaveText('3 / 16 · Ship smaller');
});

test('progress reflects slide number', async ({ mount, page }) => {
  const component = await mount(slide({ number: 3, total: 16 }));
  const progress = page.getByTestId('slide-progress');
  await expect(progress).toHaveCSS('width', '360px');

  await component.update(slide({ number: 16, total: 16 }));
  await expect(progress).toHaveCSS('width', '1920px');
});

test('chrome sits where the layouts expect it, in canvas px', async ({ mount, page }) => {
  await mount(slide());

  expect(await page.getByTestId('slide-frame').boundingBox()).toMatchObject({
    x: 0,
    y: 0,
    width: 1920,
    height: 1080,
  });
  // The footer's text line starts at y = 1023; layout bodies may run to y = 996.
  expect(await page.getByTestId('slide-footer').boundingBox()).toMatchObject({
    x: 120,
    y: 1023,
    width: 1680,
    height: 17,
  });
  expect(await page.getByTestId('part-tracker').boundingBox()).toMatchObject({
    x: 120,
    y: 40,
    width: 1680,
  });
  expect(await page.getByTestId('slide-progress').boundingBox()).toMatchObject({
    x: 0,
    y: 1075,
    height: 5,
  });
});

test('screenshot: content and title chrome', async ({ mount, page }) => {
  await mount(
    <div>
      {slide()}
      {slide({ layout: 'title', dark: true, part: null, number: 1 })}
    </div>,
  );
  const frames = page.getByTestId('slide-frame');
  await expect(frames.nth(0)).toHaveScreenshot('slide-frame-content.png');
  await expect(frames.nth(1)).toHaveScreenshot('slide-frame-title.png');
});
