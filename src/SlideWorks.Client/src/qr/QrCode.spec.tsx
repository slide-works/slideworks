import { test, expect } from '@playwright/experimental-ct-react';
import { QrCode } from './QrCode';
import { qrMatrix } from './qrMatrix';

const TEXT = 'https://slideworks.live/482913';

test('QrCode draws exactly the encoder matrix, with a 4-module quiet zone', async ({
  mount,
  page,
}) => {
  await mount(<QrCode text={TEXT} label="Join code" />);
  const svg = page.getByTestId('qr-code');
  const matrix = qrMatrix(TEXT);
  const size = matrix.length + 8;
  await expect(svg).toHaveAttribute('viewBox', `0 0 ${size} ${size}`);

  const drawn = await svg.locator('path').getAttribute('d');
  const dark = matrix.flatMap((row, y) =>
    row.flatMap((on, x) => (on ? [`M${x + 4} ${y + 4}h1v1h-1z`] : [])),
  );
  expect(drawn).toBe(dark.join(''));
});

test('QrCode is pure black on white, in a dark look too', async ({ mount, page }) => {
  await mount(
    <div
      style={
        {
          '--sw-accent': '#ff00aa',
          color: '#ff00aa',
          width: 300,
          height: 300,
        } as React.CSSProperties
      }
    >
      <QrCode text={TEXT} label="Join code" />
    </div>,
  );
  const svg = page.getByTestId('qr-code');
  await expect(svg.locator('rect')).toHaveAttribute('fill', '#ffffff');
  await expect(svg.locator('path')).toHaveAttribute('fill', '#000000');
  await expect(svg).toHaveAttribute('aria-label', 'Join code');
});

test('QrCode encodes at error-correction level M', () => {
  // The format information's level bits after masking: M reads dark then light at row 8, columns 0 and 1.
  const matrix = qrMatrix(TEXT);
  expect(matrix[8][0]).toBe(true);
  expect(matrix[8][1]).toBe(false);
});
