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

// Cobalt's solid panel surfaces; the ink is white on all three.
const SOLID_PANELS: [string, 'accent' | 'positive' | 'negative', number[]][] = [
  ['accent', 'accent', [47, 91, 234]],
  ['positive', 'positive', [21, 128, 61]],
  ['negative', 'negative', [185, 28, 28]],
];

for (const [name, tone, surface] of SOLID_PANELS) {
  test(`on a solid ${name} panel it is night at 25% over the surface, with the ink as text`, async ({
    mount,
    page,
  }) => {
    await mount(
      <div data-testid="panel" data-fill="solid" style={toneStyle(tone, 'solid')}>
        <Pin text="Hold on to this one" />
      </div>,
    );

    const pin = page.getByTestId('pin');
    await expect(pin).toHaveCSS('color', 'rgb(255, 255, 255)');
    const background = await pin.evaluate((element) => getComputedStyle(element).backgroundColor);
    const channels = (background.match(/-?\d*\.?\d+/g) ?? []).slice(0, 3).map(Number);
    const scale = background.startsWith('color(') ? 255 : 1;
    const mixed = surface.map((channel, i) => Math.round([11, 16, 32][i]! * 0.25 + channel * 0.75));
    // The browser may round a .5 either way.
    channels.forEach((channel, i) =>
      expect(Math.abs(channel * scale - mixed[i]!)).toBeLessThanOrEqual(1),
    );
  });
}

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
