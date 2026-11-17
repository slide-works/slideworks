import { test, expect } from '@playwright/experimental-ct-react';
import { presenterView } from '../test-support/fixtures/presenting';
import { SlidesSheet } from './SlidesSheet';

test('a row per slide jumps to that slide', async ({ mount, page }) => {
  const jumps: number[] = [];
  const view = presenterView();
  await mount(
    <SlidesSheet
      view={view}
      current={8}
      skipDeepDives={false}
      onJump={(n) => jumps.push(n)}
      onClose={() => {}}
    />,
  );
  await expect(page.getByTestId('slides-sheet')).toBeVisible();
  await expect(page.getByTestId(/^slides-sheet-row-\d+$/)).toHaveCount(view.slides.length);
  await page.getByTestId('slides-sheet-row-12').click();
  await expect.poll(() => jumps).toEqual([12]);
});
