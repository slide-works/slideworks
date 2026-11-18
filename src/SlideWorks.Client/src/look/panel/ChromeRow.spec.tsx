import { test, expect } from '@playwright/experimental-ct-react';
import type { ChromeOverrides } from '../../api/types';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { ChromeRow } from './ChromeRow';

const chrome = lookViewOf('talk', 'cobalt').template.chrome;

// A plain function, not a component: CT can only mount components from source files.
function row(topBar: 'none' | 'accent', onChange: (chrome: ChromeOverrides) => void) {
  return <ChromeRow chrome={chrome} topBar={topBar} disabled={false} onChange={onChange} />;
}

test('sends accent when the box is ticked', async ({ mount, page }) => {
  const sent: ChromeOverrides[] = [];
  await mount(row('none', (change) => sent.push(change)));

  const box = page.getByTestId('look-top-bar');
  await expect(box).not.toBeChecked();
  await box.click();
  expect(sent).toEqual([{ topBar: 'accent' }]);
});

test('sends none when it is cleared', async ({ mount, page }) => {
  const sent: ChromeOverrides[] = [];
  await mount(row('accent', (change) => sent.push(change)));

  const box = page.getByTestId('look-top-bar');
  await expect(box).toBeChecked();
  await box.click();
  expect(sent).toEqual([{ topBar: 'none' }]);
});

test('keeps focus on the box', async ({ mount, page }) => {
  await mount(row('none', () => undefined));

  const box = page.getByTestId('look-top-bar');
  await box.click();
  await expect(box).toBeFocused();
  await expect(page.getByLabel('Accent bar across the top')).toBeFocused();
});
