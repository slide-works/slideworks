import { test, expect } from '@playwright/experimental-ct-react';
import { Callout } from './Callout';

test('callout tones', async ({ mount, page }) => {
  await mount(
    <div style={{ width: 800 }}>
      <Callout tone="info" text="An info callout" />
      <Callout tone="good" text="A good callout" />
      <Callout tone="bad" text="A bad callout" />
    </div>,
  );

  const callouts = page.getByTestId('block-callout');
  await expect(callouts).toHaveText(['An info callout', 'A good callout', 'A bad callout']);
  await expect(callouts.nth(0)).toHaveAttribute('data-tone', 'info');
  await expect(callouts.nth(0)).toHaveCSS('background-color', 'rgb(228, 234, 253)');
  await expect(callouts.nth(0)).toHaveCSS('color', 'rgb(30, 58, 138)');
  await expect(callouts.nth(0)).toHaveCSS('box-shadow', 'none');

  await expect(callouts.nth(1)).toHaveAttribute('data-tone', 'good');
  await expect(callouts.nth(1)).toHaveCSS('background-color', 'rgb(220, 252, 231)');
  await expect(callouts.nth(1)).toHaveCSS('box-shadow', 'rgb(22, 163, 74) 6px 0px 0px 0px inset');

  await expect(callouts.nth(2)).toHaveAttribute('data-tone', 'bad');
  await expect(callouts.nth(2)).toHaveCSS('background-color', 'rgb(253, 236, 236)');
  await expect(callouts.nth(2)).toHaveCSS('box-shadow', 'rgb(220, 38, 38) 6px 0px 0px 0px inset');
});

test('callout text is plain text, never markup', async ({ mount, page }) => {
  await mount(<Callout tone="info" text="<b>not bold</b> **nor this**" />);
  await expect(page.getByTestId('block-callout')).toHaveText('<b>not bold</b> **nor this**');
  await expect(page.getByTestId('block-callout').locator('b')).toHaveCount(0);
});

test('screenshot: callout tones', async ({ mount }) => {
  const component = await mount(
    <div style={{ width: 800, padding: 20, background: '#f7f8fa' }}>
      <Callout tone="info" text="An info callout" />
      <Callout tone="good" text="A good callout" />
      <Callout tone="bad" text="A bad callout" />
    </div>,
  );
  await expect(component).toHaveScreenshot('callout-tones.png');
});
