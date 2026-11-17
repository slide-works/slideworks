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

test('rows are grouped under their part headings', async ({ mount, page }) => {
  const view = presenterView();
  await mount(
    <SlidesSheet
      view={view}
      current={1}
      skipDeepDives={false}
      onJump={() => {}}
      onClose={() => {}}
    />,
  );
  await expect(page.getByTestId('slides-sheet-group')).toHaveCount(view.parts.length);
  await expect(page.getByRole('heading', { name: view.parts[0] })).toBeVisible();
});

test('the current row is focused and in view on open', async ({ mount, page }) => {
  await mount(
    <div style={{ height: 400 }}>
      <SlidesSheet
        view={presenterView()}
        current={12}
        skipDeepDives={false}
        onJump={() => {}}
        onClose={() => {}}
      />
    </div>,
  );
  await expect(page.getByTestId('slides-sheet-row-12')).toBeFocused();
  await expect(page.getByTestId('slides-sheet-row-12')).toHaveAttribute('aria-current', 'true');
  await expect(page.getByTestId('slides-sheet-row-12')).toBeInViewport();
});

test('focus starts on Done when the current slide has no row', async ({ mount, page }) => {
  await mount(
    <SlidesSheet
      view={presenterView()}
      current={99}
      skipDeepDives={false}
      onJump={() => {}}
      onClose={() => {}}
    />,
  );
  await expect(page.getByRole('button', { name: 'Close slides' })).toBeFocused();
});

test('deep-dive rows say Deep dive, or Deep dive · skipping when skipping', async ({
  mount,
  page,
}) => {
  const view = presenterView();
  await mount(
    <SlidesSheet
      view={view}
      current={1}
      skipDeepDives={false}
      onJump={() => {}}
      onClose={() => {}}
    />,
  );
  await expect(page.getByTestId('slides-sheet-deep-dive')).toHaveCount(3);
  await expect(page.getByTestId('slides-sheet-deep-dive').first()).toHaveText('Deep dive');
  await expect(page.getByTestId('slides-sheet-deep-dive').first()).not.toHaveText(
    'Deep dive · skipping',
  );
});

test('deep-dive rows say skipping and are dimmed when skipping deep dives', async ({
  mount,
  page,
}) => {
  await mount(
    <SlidesSheet
      view={presenterView()}
      current={1}
      skipDeepDives={true}
      onJump={() => {}}
      onClose={() => {}}
    />,
  );
  await expect(page.getByTestId('slides-sheet-deep-dive').first()).toHaveText(
    'Deep dive · skipping',
  );
  await expect(page.getByTestId('slides-sheet-row-5')).toHaveCSS('opacity', '0.5');
  await expect(page.getByTestId('slides-sheet-row-6')).toHaveCSS('opacity', '1');
});

test('a row is named for its slide and headline, and jumps then closes', async ({
  mount,
  page,
}) => {
  const events: string[] = [];
  const view = presenterView();
  const headline = view.slides[7].headline;
  await mount(
    <SlidesSheet
      view={view}
      current={1}
      skipDeepDives={false}
      onJump={(n) => events.push(`jump ${n}`)}
      onClose={() => events.push('close')}
    />,
  );
  await page.getByRole('button', { name: `Jump to slide 8: ${headline}` }).click();
  await expect.poll(() => events).toEqual(['jump 8', 'close']);
});

test('Done closes the sheet without a jump', async ({ mount, page }) => {
  const events: string[] = [];
  await mount(
    <SlidesSheet
      view={presenterView()}
      current={1}
      skipDeepDives={false}
      onJump={(n) => events.push(`jump ${n}`)}
      onClose={() => events.push('close')}
    />,
  );
  await page.getByRole('button', { name: 'Close slides' }).click();
  await expect.poll(() => events).toEqual(['close']);
  await expect(page.getByText('Slides', { exact: true })).toBeVisible();
});

test('reduced motion turns the row transitions off', async ({ mount, page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await mount(
    <SlidesSheet
      view={presenterView()}
      current={1}
      skipDeepDives={false}
      onJump={() => {}}
      onClose={() => {}}
    />,
  );
  await expect(page.getByTestId('slides-sheet-row-2')).toHaveCSS('transition-duration', '0s');
});

test('the sheet at phone width, screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mount(
    <SlidesSheet
      view={presenterView()}
      current={8}
      skipDeepDives={true}
      onJump={() => {}}
      onClose={() => {}}
    />,
  );
  await expect(page.getByTestId('slides-sheet-row-8')).toBeFocused();
  await expect(page).toHaveScreenshot('slides-sheet-390.png');
});
