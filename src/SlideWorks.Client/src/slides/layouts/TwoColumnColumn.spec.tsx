import { test, expect } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright/test';
import type { ColumnSlot } from '../slots/blocks';
import { TwoColumnColumn } from './TwoColumnColumn';

// One column of the two-column layout (the levels layout places it too): its
// title with an icon beside it, and items whose icon stands where the bullet
// mark was.

function column(slot: ColumnSlot) {
  return (
    <div data-testid="host" style={{ width: 760 }}>
      <TwoColumnColumn side="left" column={slot} />
    </div>
  );
}

// What an item draws before its text: the bullet mark's content, and its indent.
const markOf = (item: Locator) =>
  item.evaluate((element) => ({
    content: getComputedStyle(element, '::before').content,
    indent: getComputedStyle(element).paddingLeft,
  }));

test("an item's icon takes the mark's place", async ({ mount, page }) => {
  await mount(
    column({
      items: [
        { icon: 'coins', lead: 'Cost.', text: 'Each slice is cheap to try.' },
        { lead: 'Risk.', text: 'A small change is easy to undo.' },
        { icon: 'shield', text: 'Nothing waits for a big launch.' },
        'A plain point',
      ],
    }),
  );

  const items = page.getByTestId('two-column-left').getByTestId('two-column-item');
  await expect(items).toHaveText([
    'Cost. Each slice is cheap to try.',
    'Risk. A small change is easy to undo.',
    'Nothing waits for a big launch.',
    'A plain point',
  ]);
  await expect(page.getByTestId('two-column-item-icon')).toHaveCount(2);

  // With an icon: a 34 px drawing at the item's left edge, on its first line, and no mark.
  for (const [index, name] of [
    [0, 'coins'],
    [2, 'shield'],
  ] as const) {
    const item = items.nth(index);
    const icon = item.getByTestId('two-column-item-icon').getByTestId('block-icon');
    await expect(icon).toHaveAttribute('data-icon', name);
    expect(await markOf(item)).toEqual({ content: 'none', indent: '68px' });
    const itemBox = (await item.boundingBox())!;
    const iconBox = (await icon.boundingBox())!;
    expect(iconBox.width).toBe(34);
    expect(iconBox.height).toBe(34);
    expect(iconBox.x).toBe(itemBox.x);
    expect(iconBox.y - itemBox.y).toBe(3);
    // The icon is drawn in the accent, as the mark was (cobalt's).
    await expect(icon).toHaveCSS('stroke', 'rgb(47, 91, 234)');
  }

  // Without one: the mark, at the indent it always had.
  for (const index of [1, 3]) {
    const item = items.nth(index);
    await expect(item.getByTestId('two-column-item-icon')).toHaveCount(0);
    expect(await markOf(item)).toEqual({ content: '""', indent: '52px' });
  }
  // The lead is still bold where there is one, with or without an icon.
  await expect(items.nth(0).locator('b')).toHaveText('Cost.');
  await expect(items.nth(1).locator('b')).toHaveText('Risk.');
});

test('icon beside the column title', async ({ mount, page }) => {
  await mount(column({ title: 'What it costs', icon: 'coins', text: 'One launch a quarter.' }));

  const title = page.getByTestId('two-column-title');
  await expect(title).toHaveText('What it costs');
  await expect(page.getByRole('heading', { level: 3 })).toHaveText('What it costs');
  const icon = page.getByTestId('two-column-title-icon').getByTestId('block-icon');
  await expect(icon).toHaveAttribute('data-icon', 'coins');
  await expect(icon).toHaveCSS('stroke', 'rgb(47, 91, 234)');

  const titleBox = (await title.boundingBox())!;
  const iconBox = (await icon.boundingBox())!;
  expect(iconBox.width).toBe(34);
  expect(iconBox.height).toBe(34);
  // At the title's left edge, inside its first line, with the text after it.
  expect(iconBox.x).toBe(titleBox.x);
  expect(iconBox.y).toBeGreaterThanOrEqual(titleBox.y);
  expect(iconBox.y + iconBox.height).toBeLessThanOrEqual(titleBox.y + titleBox.height);
  const textLeft = await title.evaluate((element) => {
    const range = document.createRange();
    range.selectNodeContents(element.lastElementChild!);
    return range.getBoundingClientRect().left;
  });
  expect(textLeft).toBe(iconBox.x + 34 + 14);
  // One line of 34 px/1.2, as a title without an icon.
  expect(titleBox.height).toBeCloseTo(34 * 1.2, 0);
});

test('a long title with an icon wraps beside the icon, inside the column', async ({
  mount,
  page,
}) => {
  await mount(
    column({
      title: 'What every big launch costs the team and everyone waiting on it',
      icon: 'coins',
      text: 'One launch a quarter.',
    }),
  );
  const host = (await page.getByTestId('host').boundingBox())!;
  const title = (await page.getByTestId('two-column-title').boundingBox())!;
  const icon = (await page.getByTestId('two-column-title-icon').boundingBox())!;
  expect(title.height).toBeGreaterThan(34 * 1.2 * 1.5);
  expect(title.x + title.width).toBeLessThanOrEqual(host.x + host.width);
  // The icon stays on the first line.
  expect(icon.y - title.y).toBe(3);
});

test('an item with only text draws no lead', async ({ mount, page }) => {
  await mount(column({ items: [{ text: 'Just the text' }, { lead: 'Lead.', text: 'And text' }] }));

  const items = page.getByTestId('two-column-item');
  await expect(items).toHaveText(['Just the text', 'Lead. And text']);
  await expect(items.nth(0).locator('b')).toHaveCount(0);
  await expect(items.nth(0).locator('*')).toHaveCount(0);
  await expect(items.nth(1).locator('b')).toHaveText('Lead.');
  await expect(page.getByTestId('two-column-item-icon')).toHaveCount(0);
  expect(await markOf(items.nth(0))).toEqual({ content: '""', indent: '52px' });
});

test('a column without icons draws as before', async ({ mount, page }) => {
  await mount(
    column({
      title: 'Before',
      items: ['A plain point', { lead: 'Weekly:', text: 'a slice a week.' }],
    }),
  );

  const title = page.getByTestId('two-column-title');
  await expect(title).toHaveText('Before');
  // The title holds its bare text and each item only its lead.
  await expect(title.locator('*')).toHaveCount(0);
  await expect(page.getByTestId('two-column-title-icon')).toHaveCount(0);
  await expect(page.getByTestId('two-column-item-icon')).toHaveCount(0);
  await expect(page.getByTestId('block-icon')).toHaveCount(0);
  const items = page.getByTestId('two-column-item');
  await expect(items).toHaveText(['A plain point', 'Weekly: a slice a week.']);
  await expect(items.nth(0).locator('*')).toHaveCount(0);
  await expect(items.nth(1).locator('*')).toHaveCount(1);
  for (const index of [0, 1]) {
    expect(await markOf(items.nth(index))).toEqual({ content: '""', indent: '52px' });
  }
  expect((await title.boundingBox())!.height).toBeCloseTo(34 * 1.2, 0);
});

test('an icon on a column with no title draws nothing', async ({ mount, page }) => {
  await mount(column({ icon: 'coins', text: 'One launch a quarter.' }));
  await expect(page.getByTestId('two-column-text')).toHaveText('One launch a quarter.');
  await expect(page.getByTestId('two-column-title')).toHaveCount(0);
  await expect(page.getByTestId('block-icon')).toHaveCount(0);
});
