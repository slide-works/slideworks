import { test, expect } from '@playwright/experimental-ct-react';
import type { LevelsBlock } from '../slots';
import { Levels } from './Levels';

// The levels block: a ladder of 2–7 rungs in a 480 px card, with any rung current.

function host(levels: LevelsBlock) {
  return (
    <div data-testid="host" style={{ width: 600, padding: 20 }}>
      <Levels levels={levels} />
    </div>
  );
}

const rung = (n: number) => ({ text: `Rung ${n}`, mark: String(n) });

test('2 rungs and the maximum', async ({ mount, page }) => {
  const component = await mount(host({ items: [rung(1), rung(2)] }));
  await expect(page.getByTestId('levels-rung')).toHaveCount(2);
  await expect(page.getByTestId('block-levels')).toHaveCSS('width', '480px');
  await component.update(host({ title: 'Maturity', items: [1, 2, 3, 4, 5, 6, 7].map(rung) }));
  await expect(page.getByTestId('levels-rung')).toHaveCount(7);
  await expect(page.getByTestId('levels-title')).toHaveText('Maturity');
  await expect(page.getByTestId('levels-title')).toHaveCSS('text-transform', 'uppercase');
  // Every rung is at least 64 px tall and 8 px from the next.
  const boxes = await page
    .getByTestId('levels-rung')
    .evaluateAll((els) => els.map((el) => el.getBoundingClientRect()));
  for (const box of boxes) expect(box.height).toBeGreaterThanOrEqual(64);
  expect(boxes[1].top - boxes[0].bottom).toBeCloseTo(8, 0);
});

test('a current rung carries aria-current', async ({ mount, page }) => {
  await mount(
    host({
      items: [
        { text: 'Plain', mark: '1' },
        { text: 'Here', mark: '2', state: 'current' },
        { text: 'Also here', mark: '3', state: 'current' },
      ],
    }),
  );
  const rungs = page.getByTestId('levels-rung');
  await expect(rungs.nth(0)).not.toHaveAttribute('aria-current', /.*/);
  await expect(rungs.nth(1)).toHaveAttribute('aria-current', 'true');
  await expect(rungs.nth(2)).toHaveAttribute('aria-current', 'true');
  await expect(rungs.nth(1)).toHaveCSS('background-color', 'rgb(228, 234, 253)');
  await expect(rungs.nth(1)).toHaveCSS('color', 'rgb(30, 58, 138)');
  await expect(rungs.nth(1)).toHaveCSS('font-weight', '700');
  await expect(rungs.nth(1).getByTestId('levels-mark')).toHaveCSS(
    'background-color',
    'rgb(47, 91, 234)',
  );
  await expect(rungs.nth(0)).toHaveCSS('background-color', 'rgba(0, 0, 0, 0)');
});

test('a rung without a mark draws an empty circle', async ({ mount, page }) => {
  await mount(host({ items: [{ text: 'No mark' }, rung(2)] }));
  const mark = page.getByTestId('levels-mark').first();
  await expect(mark).toHaveText('');
  const box = (await mark.boundingBox())!;
  expect(box.width).toBeCloseTo(44, 0);
  expect(box.height).toBeCloseTo(44, 0);
  await expect(mark).toHaveCSS('border-top-left-radius', '999px');
});

test('a card clips a too-wide rung instead of drawing it over its neighbours', async ({
  mount,
  page,
}) => {
  await mount(host({ items: [rung(1), rung(2)] }));
  await expect(page.getByTestId('block-levels')).toHaveCSS('overflow', 'hidden');
});

test('screenshot: levels block', async ({ mount }) => {
  const component = await mount(
    host({
      title: 'Maturity',
      items: [
        { text: 'Continuous flow', mark: '4' },
        { text: 'Small batches', mark: '3', state: 'current' },
        { text: 'Quarterly releases', mark: '2' },
        { text: 'Big bang', mark: '1' },
      ],
    }),
  );
  await expect(component).toHaveScreenshot('block-levels.png');
});
