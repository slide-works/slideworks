import { test, expect } from '@playwright/experimental-ct-react';
import { presenterView } from '../test-support/fixtures/presenting';
import { Overview } from './Overview';

// The sample deck: 16 slides in four parts (1-6, 7-11, 12-13, 14-16), deep dives 5, 10 and 12.
async function open(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  props: { current?: number; skip?: boolean; jumps?: number[]; closes?: number[] } = {},
) {
  return mount(
    <Overview
      view={presenterView()}
      current={props.current ?? 8}
      skipDeepDives={props.skip ?? false}
      onJump={(n) => props.jumps?.push(n)}
      onClose={() => props.closes?.push(1)}
    />,
  );
}

test('groups the slides by part heading, in deck order', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await open(mount);

  await expect(page.getByTestId('overview-part')).toHaveText([
    'The cost',
    'Slice it',
    'Ship it',
    'Make it stick',
  ]);
  const groups = page.getByTestId('overview-group');
  await expect(groups.nth(0).getByTestId(/^overview-slide-\d+$/)).toHaveCount(6);
  await expect(groups.nth(1).getByTestId(/^overview-slide-\d+$/)).toHaveCount(5);
  await expect(groups.nth(2).getByTestId(/^overview-slide-\d+$/)).toHaveCount(2);
  await expect(groups.nth(3).getByTestId(/^overview-slide-\d+$/)).toHaveCount(3);
  await expect(page.getByTestId(/^overview-slide-\d+$/).first()).toHaveAttribute(
    'data-testid',
    'overview-slide-1',
  );
  await expect(page.getByTestId('overview-slide-8')).toHaveAccessibleName(
    'Jump to slide 8: Slice by what a customer can do, not by layer',
  );
});

test('lays out five thumbnails per row, three under 900 px', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await open(mount);
  const firstGroup = page
    .getByTestId('overview-group')
    .first()
    .getByTestId(/^overview-slide-\d+$/);
  const rowTops = async () =>
    new Set(
      await firstGroup.evaluateAll((tiles) =>
        tiles.map((t) => Math.round(t.getBoundingClientRect().top)),
      ),
    ).size;

  expect(await rowTops()).toBe(2);
  const firstRow = await firstGroup.evaluateAll(
    (tiles) =>
      tiles.filter((t) => t.getBoundingClientRect().top === tiles[0].getBoundingClientRect().top)
        .length,
  );
  expect(firstRow).toBe(5);

  await page.setViewportSize({ width: 800, height: 800 });
  const narrowRow = await firstGroup.evaluateAll(
    (tiles) =>
      tiles.filter((t) => t.getBoundingClientRect().top === tiles[0].getBoundingClientRect().top)
        .length,
  );
  expect(narrowRow).toBe(3);
});

test('marks the current slide with a Now pill and deep dives with a pill', async ({
  mount,
  page,
}) => {
  await open(mount, { current: 8 });

  await expect(page.getByTestId('overview-now')).toHaveCount(1);
  await expect(page.getByTestId('overview-slide-8').getByTestId('overview-now')).toHaveText('Now');
  await expect(page.getByTestId('overview-slide-8')).toHaveAttribute('aria-current', 'true');
  await expect(page.getByTestId('overview-slide-7')).not.toHaveAttribute('aria-current', 'true');
  await expect(page.getByTestId('overview-deep-dive')).toHaveCount(3);
  for (const n of [5, 10, 12])
    await expect(
      page.getByTestId(`overview-slide-${n}`).getByTestId('overview-deep-dive'),
    ).toHaveCount(1);
});

test('dims the deep dives only while skipping is on', async ({ mount, page }) => {
  const component = await open(mount, { skip: false });
  const frame = (n: number) =>
    page.getByTestId(`overview-slide-${n}`).getByTestId('overview-frame');

  await expect(frame(5)).toHaveCSS('opacity', '1');
  await expect(page.getByTestId('overview-slide-5')).not.toHaveAttribute('data-skipped', 'true');

  await component.update(
    <Overview
      view={presenterView()}
      current={8}
      skipDeepDives
      onJump={() => {}}
      onClose={() => {}}
    />,
  );
  for (const n of [5, 10, 12]) {
    await expect(page.getByTestId(`overview-slide-${n}`)).toHaveAttribute('data-skipped', 'true');
    await expect(frame(n)).toHaveCSS('opacity', '0.3');
  }
  await expect(frame(6)).toHaveCSS('opacity', '1');
  await expect(page.getByTestId('overview-slide-5')).toBeEnabled();
});

test('a click jumps to that slide, even a dimmed deep dive', async ({ mount, page }) => {
  const jumps: number[] = [];
  await open(mount, { skip: true, jumps });
  await page.getByTestId('overview-slide-3').click();
  await expect.poll(() => jumps).toEqual([3]);
  await page.getByTestId('overview-slide-10').click();
  await expect.poll(() => jumps).toEqual([3, 10]);
});

test('Enter on the focused thumbnail jumps to it, arrows move the focus', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  const jumps: number[] = [];
  await open(mount, { current: 8, jumps });

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('overview-slide-9')).toBeFocused();
  await page.keyboard.press('ArrowLeft');
  await page.keyboard.press('ArrowLeft');
  await expect(page.getByTestId('overview-slide-7')).toBeFocused();
  await page.keyboard.press('ArrowUp');
  await expect(page.getByTestId('overview-slide-6')).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByTestId('overview-slide-7')).toBeFocused();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('overview-slide-8')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect.poll(() => jumps).toEqual([8]);
});

test('Escape calls onClose once', async ({ mount, page }) => {
  const closes: number[] = [];
  await open(mount, { current: 1, closes });
  await expect(page.getByTestId('overview')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect.poll(() => closes.length).toBe(1);
  await page.waitForTimeout(100);
  expect(closes.length).toBe(1);
});

test('focus starts on the current slide', async ({ mount, page }) => {
  await open(mount, { current: 11 });
  await expect(page.getByTestId('overview-slide-11')).toBeFocused();
});

test('Overview screenshot of the sample deck on slide 8', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await open(mount, { current: 8, skip: true });
  await expect(page.getByTestId('overview-slide-8')).toBeFocused();
  await expect(page).toHaveScreenshot('overview.png');
});
