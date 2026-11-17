import { test, expect } from '@playwright/experimental-ct-react';
import { presenterView } from '../test-support/fixtures/presenting';
import { Overview } from './Overview';

test('a button per slide jumps to that slide', async ({ mount, page }) => {
  const jumps: number[] = [];
  const view = presenterView();
  await mount(
    <Overview
      view={view}
      current={8}
      skipDeepDives={false}
      onJump={(n) => jumps.push(n)}
      onClose={() => {}}
    />,
  );
  await expect(page.getByTestId('overview')).toBeVisible();
  await expect(page.getByTestId(/^overview-slide-\d+$/)).toHaveCount(view.slides.length);
  await page.getByTestId('overview-slide-3').click();
  await expect.poll(() => jumps).toEqual([3]);
});

test('escape closes the overview', async ({ mount, page }) => {
  let closed = 0;
  await mount(
    <Overview
      view={presenterView()}
      current={1}
      skipDeepDives={false}
      onJump={() => {}}
      onClose={() => closed++}
    />,
  );
  await expect(page.getByTestId('overview')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect.poll(() => closed).toBe(1);
});
