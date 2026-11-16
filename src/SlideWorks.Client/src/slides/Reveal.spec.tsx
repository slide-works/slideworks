import { test, expect } from '@playwright/experimental-ct-react';
import { Reveal } from './Reveal';

test('a hidden item keeps its layout box, hidden by visibility and opacity', async ({
  mount,
  page,
}) => {
  await mount(
    <div>
      <Reveal at={1} step={1}>
        <span data-testid="shown">First point</span>
      </Reveal>
      <Reveal at={2} step={1}>
        <span data-testid="waiting">Second point</span>
      </Reveal>
    </div>,
  );

  const waiting = page.getByTestId('waiting');
  await expect(page.getByTestId('shown')).toBeVisible();
  await expect(waiting).toBeHidden();
  const box = await waiting.boundingBox();
  expect(box?.width).toBeGreaterThan(0);
  expect(box?.height).toBeGreaterThan(0);
  const style = await waiting.evaluate((el) => {
    const reveal = getComputedStyle(el.parentElement!);
    return { display: reveal.display, visibility: reveal.visibility, opacity: reveal.opacity };
  });
  expect(style).toEqual({ display: 'block', visibility: 'hidden', opacity: '0' });
});

test('an item shows once its step comes', async ({ mount, page }) => {
  await mount(
    <Reveal at={2} step={2} as="li">
      <span data-testid="item">Second point</span>
    </Reveal>,
  );
  await expect(page.getByTestId('item')).toBeVisible();
});
