import { test, expect } from '@playwright/experimental-ct-react';
import { presenterView } from '../test-support/fixtures/presenting';
import { PhoneConnectDialog } from './PhoneConnectDialog';
import { qrPath } from './QrCode';
import type { PresentationConnection } from './types';

const phone: PresentationConnection = { clientId: 'p1', role: 'phone', label: "Sam's iPhone" };

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
  expect(drawn).toBe(qrPath(remoteUrl).path);
  expect(drawn).not.toBe(qrPath(`${remoteUrl}x`).path);
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
