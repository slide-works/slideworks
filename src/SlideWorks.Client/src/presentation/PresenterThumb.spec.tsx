import { test, expect } from '@playwright/experimental-ct-react';
import { presenterView } from '../test-support/fixtures/presenting';
import { PresenterThumb } from './PresenterThumb';

// Slide 3 of the sample deck is the bullets slide, with four reveal steps.
test('PresenterThumb renders the slide at the given step, scaled to its width', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 480, height: 600 });
  await mount(<PresenterThumb view={presenterView()} slide={3} step={2} />);
  const thumb = page.getByTestId('presenter-thumb');

  await expect(thumb).toHaveAttribute('data-slide', '3');
  await expect(thumb.getByTestId('player-slide-3')).toHaveCount(1);
  const items = thumb.getByTestId('bullets-item');
  await expect(items).toHaveCount(4);
  await expect(items.nth(1)).toHaveCSS('visibility', 'visible');
  await expect(items.nth(2)).toHaveCSS('visibility', 'hidden');
  const box = await thumb.boundingBox();
  expect(box!.width).toBe(480);
  expect(box!.height).toBe(270);
  // The 1920-wide canvas is scaled down to the thumbnail's width.
  const canvas = await thumb.getByTestId('player-slide-3').boundingBox();
  expect(Math.round(canvas!.width)).toBe(480);
});

test('PresenterThumb starts at step 0 and is a picture: inert and hidden from assistive tech', async ({
  mount,
  page,
}) => {
  await mount(<PresenterThumb view={presenterView()} slide={3} />);
  const thumb = page.getByTestId('presenter-thumb');

  await expect(thumb).toHaveAttribute('aria-hidden', 'true');
  await expect(thumb).toHaveAttribute('inert', '');
  await expect(thumb.getByTestId('bullets-item').first()).toHaveCSS('visibility', 'hidden');
});

test('PresenterThumb screenshot of the title slide', async ({ mount, page }) => {
  await page.setViewportSize({ width: 480, height: 300 });
  await mount(<PresenterThumb view={presenterView()} slide={1} />);

  await expect(page.getByTestId('presenter-thumb')).toHaveScreenshot('presenter-thumb-title.png');
});
