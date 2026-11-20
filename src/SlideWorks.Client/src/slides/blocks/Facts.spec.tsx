import { test, expect } from '@playwright/experimental-ct-react';
import type { FactsBlock } from '../slots';
import { Facts } from './Facts';

// The facts block: 2–4 equal tiles, each a label, a value and a rule in its tone.

function host(facts: FactsBlock) {
  return (
    <div data-testid="host" style={{ width: 1144 }}>
      <Facts facts={facts} />
    </div>
  );
}

const tile = (n: number) => ({ label: `Label ${n}`, value: `Value ${n}` });

test('2 and 4 tiles', async ({ mount, page }) => {
  const component = await mount(host([tile(1), tile(2)]));
  let widths = await page
    .getByTestId('facts-tile')
    .evaluateAll((els) => els.map((el) => el.getBoundingClientRect().width));
  expect(widths).toHaveLength(2);
  expect(widths[0]).toBeCloseTo(widths[1], 0);
  expect(widths[0]).toBeCloseTo((1144 - 20) / 2, 0);
  await component.update(host([tile(1), tile(2), tile(3), tile(4)]));
  const boxes = await page
    .getByTestId('facts-tile')
    .evaluateAll((els) => els.map((el) => el.getBoundingClientRect()));
  widths = boxes.map((box) => box.width);
  expect(widths).toHaveLength(4);
  for (const width of widths) expect(width).toBeCloseTo((1144 - 60) / 4, 0);
  expect(boxes[1].left - boxes[0].right).toBeCloseTo(20, 0);
  for (const box of boxes) expect(box.height).toBeGreaterThanOrEqual(120);
  await expect(page.getByTestId('facts-label').first()).toHaveText('Label 1');
  await expect(page.getByTestId('facts-value').first()).toHaveText('Value 1');
});

test('a toned tile colours its top rule', async ({ mount, page }) => {
  await mount(host([{ ...tile(1), tone: 'accent' }, { ...tile(2), tone: 'negative' }, tile(3)]));
  const tiles = page.getByTestId('facts-tile');
  await expect(tiles.first()).toHaveCSS('border-top-width', '5px');
  await expect(tiles.nth(0)).toHaveCSS('border-top-color', 'rgb(47, 91, 234)');
  await expect(tiles.nth(1)).toHaveCSS('border-top-color', 'rgb(220, 38, 38)');
  await expect(tiles.nth(2)).toHaveCSS('border-top-color', 'rgb(227, 230, 236)');
});

test('a tile has no icon and the row clips its overflow', async ({ mount, page }) => {
  await mount(host([tile(1), tile(2)]));
  await expect(page.getByTestId('block-facts').locator('svg')).toHaveCount(0);
  await expect(page.getByTestId('block-facts')).toHaveCSS('overflow', 'hidden');
});

test('screenshot: facts block', async ({ mount }) => {
  const component = await mount(
    host([
      { label: 'Cycle time', value: '9 days', tone: 'accent' },
      { label: 'Slips', value: '2 a year', tone: 'negative' },
      { label: 'Releases', value: 'Weekly', tone: 'positive' },
      { label: 'Team', value: 'Eight people' },
    ]),
  );
  await expect(component).toHaveScreenshot('block-facts.png');
});
