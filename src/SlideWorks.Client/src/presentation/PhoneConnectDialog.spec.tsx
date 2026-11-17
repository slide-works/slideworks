import { test, expect } from '@playwright/experimental-ct-react';
import QRCode from 'qrcode';
import { presenterView } from '../test-support/fixtures/presenting';
import { PhoneConnectDialog } from './PhoneConnectDialog';
import type { PresentationConnection } from './types';

const phone: PresentationConnection = { clientId: 'p1', role: 'phone', label: "Sam's iPhone" };

// What the encoder makes for a url, drawn the way QrCode draws it, so a spec can tell which url a code holds.
function pathFor(url: string): string {
  const { modules } = QRCode.create(url, { errorCorrectionLevel: 'M' });
  let path = '';
  for (let row = 0; row < modules.size; row++) {
    for (let col = 0; col < modules.size; col++) {
      if (modules.get(row, col)) path += `M${col + 2} ${row + 2}h1v1h-1z`;
    }
  }
  return path;
}

test('open, it shows the remote url', async ({ mount, page }) => {
  const { remoteUrl } = presenterView();
  await mount(<PhoneConnectDialog remoteUrl={remoteUrl} open onClose={() => {}} />);
  await expect(page.getByTestId('phone-connect-url')).toHaveText(remoteUrl);
});

test('closed, it renders nothing', async ({ mount, page }) => {
  await mount(
    <PhoneConnectDialog remoteUrl={presenterView().remoteUrl} open={false} onClose={() => {}} />,
  );
  await expect(page.getByTestId('phone-connect')).toHaveCount(0);
});

test('the QR code encodes the remote url', async ({ mount, page }) => {
  const { remoteUrl } = presenterView();
  await mount(<PhoneConnectDialog remoteUrl={remoteUrl} open onClose={() => {}} />);
  const drawn = await page.getByTestId('phone-connect-qr').locator('path').getAttribute('d');
  expect(drawn).toBe(pathFor(remoteUrl));
  expect(drawn).not.toBe(pathFor(`${remoteUrl}x`));
});

test('Copy puts the remote url on the clipboard', async ({ mount, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  const { remoteUrl } = presenterView();
  await mount(<PhoneConnectDialog remoteUrl={remoteUrl} open onClose={() => {}} />);
  await page.getByTestId('phone-connect-copy').click();
  await expect(page.getByTestId('phone-connect-copy')).toHaveText('Copied');
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(remoteUrl);
});

test('without phones, it shows no chips and stays open', async ({ mount, page }) => {
  await mount(<PhoneConnectDialog remoteUrl={presenterView().remoteUrl} open onClose={() => {}} />);
  await expect(page.getByTestId('phone-connect-chip')).toHaveCount(0);
  await expect(page.getByTestId('phone-connect')).toBeVisible();
});

test('with phones, a chip per phone shows and the first phone to connect closes it', async ({
  mount,
  page,
}) => {
  const { remoteUrl } = presenterView();
  let closed = 0;
  const onClose = () => closed++;
  const component = await mount(
    <PhoneConnectDialog remoteUrl={remoteUrl} open onClose={onClose} phones={[]} />,
  );
  await expect(page.getByTestId('phone-connect-chip')).toHaveCount(0);
  expect(closed).toBe(0);

  await component.update(
    <PhoneConnectDialog remoteUrl={remoteUrl} open onClose={onClose} phones={[phone]} />,
  );
  await expect.poll(() => closed).toBe(1);
});

test('with a phone already connected when it opens, it shows its chip and stays open', async ({
  mount,
  page,
}) => {
  let closed = 0;
  await mount(
    <PhoneConnectDialog
      remoteUrl={presenterView().remoteUrl}
      open
      onClose={() => closed++}
      phones={[phone]}
    />,
  );
  await expect(page.getByTestId('phone-connect-chip')).toHaveText("Sam's iPhone connected");
  expect(closed).toBe(0);
});

test('dialog screenshot', async ({ mount, page }) => {
  await mount(
    <PhoneConnectDialog
      remoteUrl={presenterView().remoteUrl}
      open
      onClose={() => {}}
      phones={[phone]}
    />,
  );
  await expect(page).toHaveScreenshot('phone-connect.png');
});
