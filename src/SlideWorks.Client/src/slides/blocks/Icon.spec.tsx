import { test, expect } from '@playwright/experimental-ct-react';
import iconsJson from '@contracts/icons.json' with { type: 'json' };
import { Icon } from './Icon';

// The icon block (K11): a line drawing from the built-in set, in the text
// colour of whatever places it.

test('draws the named icon at its size, in the text colour, hidden from screen readers', async ({
  mount,
  page,
}) => {
  await mount(
    <div data-testid="host" style={{ color: 'rgb(12, 34, 56)' }}>
      <Icon name="coins" size={34} />
    </div>,
  );

  const icon = page.getByTestId('block-icon');
  await expect(icon).toHaveCount(1);
  await expect(icon).toHaveAttribute('data-icon', 'coins');
  await expect(icon).toHaveAttribute('aria-hidden', 'true');
  await expect(icon).toHaveCSS('stroke', 'rgb(12, 34, 56)');
  await expect(icon).toHaveCSS('fill', 'none');
  const box = (await icon.boundingBox())!;
  expect(box.width).toBe(34);
  expect(box.height).toBe(34);
  // It is a drawing, not an empty box.
  expect(await icon.locator('path, circle, rect, line, polyline, ellipse').count()).toBeGreaterThan(
    0,
  );
});

test('follows the size it is given', async ({ mount, page }) => {
  await mount(
    <div>
      <Icon name="shield" size={48} />
    </div>,
  );
  const box = (await page.getByTestId('block-icon').boundingBox())!;
  expect(box.width).toBe(48);
  expect(box.height).toBe(48);
});

test('an unknown name draws nothing', async ({ mount, page }) => {
  await mount(
    <div data-testid="host">
      <Icon name="no-such-icon" size={34} />
      <Icon name="constructor" size={34} />
      <Icon name="<svg onload=alert(1)>" size={34} />
    </div>,
  );
  await expect(page.getByTestId('host')).toBeAttached();
  await expect(page.getByTestId('block-icon')).toHaveCount(0);
  await expect(page.getByTestId('host').locator('*')).toHaveCount(0);
});

test('every name of the set draws', async ({ mount, page }) => {
  const names = iconsJson.icons.map((icon) => icon.name);
  await mount(
    <div>
      {names.map((name) => (
        <Icon key={name} name={name} size={34} />
      ))}
    </div>,
  );
  const icons = page.getByTestId('block-icon');
  await expect(icons).toHaveCount(48);
  expect(
    await icons.evaluateAll((drawn) => drawn.map((icon) => icon.getAttribute('data-icon'))),
  ).toEqual(names);
});
