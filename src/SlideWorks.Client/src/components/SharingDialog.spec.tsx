import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { SharingView } from '../api/types';
import { sharing } from '../test-support/fixtures/screens';
import { SharingDialogHarness } from '../test-support/SharingDialogHarness';

const deckId = '7Kq2xYp9LmQa';
const sharingPath = `**/api/decks/${deckId}/sharing`;

type Answer = { status: number; json: unknown };

// Answers GET with `view` and records each PUT body, answering it with `put`
// (by default the view with the body applied).
async function stubSharing(page: Page, view: SharingView, put?: Answer) {
  const puts: unknown[] = [];
  await page.route(sharingPath, (route) => {
    const request = route.request();
    if (request.method() === 'PUT') {
      const body = request.postDataJSON() as Pick<SharingView, 'level' | 'viewers'>;
      puts.push(body);
      return route.fulfill(put ?? { status: 200, json: { ...view, ...body } });
    }
    return route.fulfill({ json: view });
  });
  return puts;
}

test('loads current level and viewers', async ({ mount, page }) => {
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByRole('dialog', { name: 'Share this deck' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('radio', { name: 'Named people' })).toBeChecked();
  await expect(dialog.getByRole('radio', { name: 'Named people' })).toBeFocused();
  await expect(dialog.getByTestId('sharing-chip-email')).toHaveText([
    'alex@team.example',
    'mira@team.example',
  ]);
  await expect(dialog.getByTestId('sharing-link')).toHaveText(sharing.link);
  await expect(dialog.getByTestId('sharing-agents-note')).toContainText(
    'Agents signed in as you can change this too, with set_sharing',
  );
  await expect(dialog.getByTestId('sharing-last-change')).toHaveText(
    'Last change: Claude, just now.',
  );
  await expect(dialog.getByTestId('sharing-qr-later')).toBeDisabled();
  await expect(dialog.getByTestId('sharing-qr-later')).toHaveText('QR join for an audience · SW-5');
});

test('switch to link and save sends PUT and calls onSaved', async ({ mount, page }) => {
  const puts = await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  await dialog.getByRole('radio', { name: 'Anyone with the link' }).check();
  await expect(dialog.getByTestId('sharing-chip')).toHaveCount(0);
  await dialog.getByTestId('sharing-save').click();

  await expect(page.getByTestId('saved-level')).toHaveText('link');
  expect(puts).toEqual([{ level: 'link', viewers: ['alex@team.example', 'mira@team.example'] }]);
  await expect(page.getByTestId('sharing-dialog')).toHaveCount(0);
  await expect(page.getByTestId('open-sharing')).toBeFocused();
});

test('add and remove email chips', async ({ mount, page }) => {
  const puts = await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const input = dialog.getByTestId('sharing-email-input');
  await input.fill('sam@team.example');
  await input.press('Enter');
  await input.pressSequentially('kai@team.example,');
  await expect(input).toHaveValue('');
  await dialog.getByRole('button', { name: 'Remove alex@team.example' }).click();
  await expect(dialog.getByTestId('sharing-chip-email')).toHaveText([
    'mira@team.example',
    'sam@team.example',
    'kai@team.example',
  ]);

  // Blank entries never become chips; an email still being typed is saved too.
  await input.fill('   ');
  await input.press('Enter');
  await expect(dialog.getByTestId('sharing-chip')).toHaveCount(3);
  await input.fill('lee@team.example');
  await dialog.getByTestId('sharing-save').click();

  await expect(page.getByTestId('saved-level')).toHaveText('named');
  expect(puts).toEqual([
    {
      level: 'named',
      viewers: ['mira@team.example', 'sam@team.example', 'kai@team.example', 'lee@team.example'],
    },
  ]);
});

test('server 400 shows message', async ({ mount, page }) => {
  const error = 'viewers: "not-an-email" is not an email address';
  await stubSharing(page, sharing, { status: 400, json: { error } });
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const input = dialog.getByTestId('sharing-email-input');
  await input.fill('not-an-email');
  await input.press('Enter');
  await dialog.getByTestId('sharing-save').click();

  await expect(dialog.getByTestId('sharing-error')).toHaveText(error);
  await expect(dialog.getByTestId('sharing-error')).toHaveAttribute('role', 'alert');
  await expect(input).toHaveAccessibleDescription(error);
  await expect(input).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByTestId('saved-level')).toHaveText('none');
  await expect(dialog.getByTestId('sharing-save')).toBeEnabled();
});

test('null lastChange hides the line', async ({ mount, page }) => {
  await stubSharing(page, { ...sharing, level: 'private', viewers: [], lastChange: null });
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  await expect(dialog.getByRole('radio', { name: 'Private' })).toBeChecked();
  await expect(dialog.getByTestId('sharing-agents-note')).toBeVisible();
  await expect(dialog.getByTestId('sharing-last-change')).toHaveCount(0);
});

test('copy link', async ({ mount, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const copy = page.getByTestId('sharing-copy-link');
  await expect(copy).toHaveText('Copy link');
  await copy.click();
  await expect(copy).toHaveText('Copied');
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(sharing.link);
});

test('copy link without a clipboard selects the link', async ({ mount, page }) => {
  await page.evaluate(() =>
    Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true }),
  );
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  await page.getByTestId('sharing-copy-link').click();
  await expect(page.getByTestId('sharing-copy-link')).toHaveText('Select and copy');
  await expect
    .poll(() => page.evaluate(() => window.getSelection()?.toString()))
    .toBe(sharing.link);
});

test('esc closes', async ({ mount, page }) => {
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();
  await expect(page.getByRole('radio', { name: 'Named people' })).toBeFocused();

  await page.keyboard.press('Escape');

  await expect(page.getByTestId('sharing-dialog')).toHaveCount(0);
  await expect(page.getByTestId('open-sharing')).toBeFocused();
  await expect(page.getByTestId('saved-level')).toHaveText('none');
});

test('cancel closes without saving and returns focus', async ({ mount, page }) => {
  const puts = await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  await page.getByRole('radio', { name: 'Private' }).check();
  await page.getByTestId('sharing-cancel').click();

  await expect(page.getByTestId('sharing-dialog')).toHaveCount(0);
  await expect(page.getByTestId('open-sharing')).toBeFocused();
  expect(puts).toEqual([]);
});

test('tab stays inside the dialog', async ({ mount, page }) => {
  await stubSharing(page, { ...sharing, level: 'link' });
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const link = dialog.getByRole('radio', { name: 'Anyone with the link' });
  await expect(link).toBeFocused();

  // Forward: the link radio, Copy link, Cancel, Save, then back to the radio.
  for (const next of ['sharing-copy-link', 'sharing-cancel', 'sharing-save']) {
    await page.keyboard.press('Tab');
    await expect(dialog.getByTestId(next)).toBeFocused();
  }
  await page.keyboard.press('Tab');
  await expect(link).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(dialog.getByTestId('sharing-save')).toBeFocused();
});

test('a failed load shows the message', async ({ mount, page }) => {
  await page.route(sharingPath, (route) =>
    route.fulfill({ status: 404, json: { error: 'Deck not found' } }),
  );
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  await expect(page.getByTestId('sharing-load-error')).toHaveText('Deck not found');
});

// The baseline comes from CI's playwright job (never from a dev machine); E2
// refreshes the baselines from its report.
test('sharing dialog screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 900, height: 720 });
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();
  await expect(page.getByTestId('sharing-chip')).toHaveCount(2);
  await expect(page.getByTestId('sharing-dialog')).toHaveScreenshot('sharing-dialog.png');
});
