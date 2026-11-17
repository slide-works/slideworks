import { test, expect } from '@playwright/experimental-ct-react';
import { presenterView } from '../test-support/fixtures/presenting';
import { PhoneConnectDialog } from './PhoneConnectDialog';

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
