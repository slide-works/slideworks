import { test, expect } from '@playwright/experimental-ct-react';
import { Pin } from './Pin';
import { toneStyle } from './tone';

// The pin block: a short label in the corner of a highlighted card.

test('shows its label', async ({ mount, page }) => {
  await mount(
    <div style={{ width: 400 }}>
      <Pin text="Hold on to this one" />
    </div>,
  );

  const pin = page.getByTestId('pin');
  await expect(pin).toHaveText('Hold on to this one');
  await expect(pin).toHaveCSS('text-transform', 'uppercase');
  await expect(pin).toHaveCSS('font-size', '15px');
  await expect(pin).toHaveCSS('font-weight', '700');
  await expect(pin).toHaveCSS('line-height', '15px');
  // 0.14em of 15 px.
  await expect(pin).toHaveCSS('letter-spacing', '2.1px');
  // Cobalt's accent-light on its accent-dark, as a pill.
  await expect(pin).toHaveCSS('color', 'rgb(228, 234, 253)');
  await expect(pin).toHaveCSS('background-color', 'rgb(30, 58, 138)');
  const box = (await pin.boundingBox())!;
  const radius = await pin.evaluate((element) =>
    parseFloat(getComputedStyle(element).borderTopLeftRadius),
  );
  expect(radius).toBeGreaterThanOrEqual(box.height / 2);
  expect(box.height).toBe(31);
});

test('the label is plain text, never markup', async ({ mount, page }) => {
  await mount(<Pin text="<b>keep</b> this" />);
  await expect(page.getByTestId('pin')).toHaveText('<b>keep</b> this');
  await expect(page.getByTestId('pin').locator('*')).toHaveCount(0);
});

test('on a solid panel it is the ink at 22% over the surface, with the ink as text', async ({
  mount,
  page,
}) => {
  await mount(
    <div data-testid="panel" data-fill="solid" style={toneStyle('accent', 'solid')}>
      <Pin text="Hold on to this one" />
    </div>,
  );

  const pin = page.getByTestId('pin');
  // Cobalt's solid accent panel: white ink on the accent.
  await expect(pin).toHaveCSS('color', 'rgb(255, 255, 255)');
  const background = await pin.evaluate((element) => getComputedStyle(element).backgroundColor);
  const channels = (background.match(/-?\d*\.?\d+/g) ?? []).slice(0, 3).map(Number);
  const scale = background.startsWith('color(') ? 255 : 1;
  const mixed = [47, 91, 234].map((channel) => Math.round(255 * 0.22 + channel * 0.78));
  expect(channels.map((channel) => Math.round(channel * scale))).toEqual(mixed);
});

test('a long label stays inside the box that places it', async ({ mount, page }) => {
  await mount(
    <div data-testid="host" style={{ width: 200 }}>
      <Pin text="Holdontothisoneforalongwhileyet" />
    </div>,
  );
  const host = (await page.getByTestId('host').boundingBox())!;
  const pin = (await page.getByTestId('pin').boundingBox())!;
  expect(pin.x + pin.width).toBeLessThanOrEqual(host.x + host.width);
});
