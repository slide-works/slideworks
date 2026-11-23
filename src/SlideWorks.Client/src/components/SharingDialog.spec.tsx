import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { SharingPeopleRequest, SharingView } from '../api/types';
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
      const body = request.postDataJSON() as SharingPeopleRequest;
      puts.push(body);
      const people = body.people.map((p) => ({ ...p, name: null, avatar: null }));
      return route.fulfill(put ?? { status: 200, json: { ...view, level: body.level, people } });
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
  await expect(dialog.getByRole('radio', { name: 'Only people below' })).toBeChecked();
  await expect(dialog.getByRole('radio', { name: 'Only people below' })).toBeFocused();
  await expect(dialog.getByTestId('sharing-person-email')).toHaveText([
    'alex@team.example',
    'mira@team.example',
  ]);
  await expect(dialog.getByTestId('sharing-link')).toHaveText(sharing.link);
  await expect(dialog.getByTestId('sharing-agents-note')).toContainText(
    'Agents signed in as you can change this too, with set_sharing. People with Can edit can use their own agents',
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
  await dialog.getByTestId('sharing-save').click();

  await expect(page.getByTestId('saved-level')).toHaveText('link');
  expect(puts).toEqual([
    {
      level: 'link',
      people: [
        { email: 'alex@team.example', access: 'write' },
        { email: 'mira@team.example', access: 'read' },
      ],
    },
  ]);
  await expect(page.getByTestId('sharing-dialog')).toHaveCount(0);
  await expect(page.getByTestId('open-sharing')).toBeFocused();
});

test('add and remove people', async ({ mount, page }) => {
  const puts = await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const input = dialog.getByTestId('sharing-email-input');
  await input.fill('Sam2@team.example');
  await input.press('Enter');
  await expect(input).toBeFocused();
  await input.fill('alex@team.example');
  await input.press('Enter');
  await expect(dialog.getByTestId('sharing-person')).toHaveCount(3);
  await dialog.getByRole('button', { name: 'Remove alex@team.example' }).click();
  await expect(input).toBeFocused();
  await expect(dialog.getByTestId('sharing-person-email')).toHaveText([
    'mira@team.example',
    'sam2@team.example',
  ]);
  await expect(dialog.getByTestId('sharing-people-count')).toHaveText('3');

  // An email still typed in full is saved too.
  await input.fill('lee@team.example');
  await dialog.getByTestId('sharing-save').click();

  await expect(page.getByTestId('saved-level')).toHaveText('named');
  expect(puts).toEqual([
    {
      level: 'named',
      people: [
        { email: 'mira@team.example', access: 'read' },
        { email: 'sam2@team.example', access: 'read' },
        { email: 'lee@team.example', access: 'read' },
      ],
    },
  ]);
});

test('SharingDialog_WhenPartialEmailTyped_ShouldSuggestNobody', async ({ mount, page }) => {
  let reads = 0;
  await page.route('**/api/**', (route) => {
    reads += route.request().url().includes('/sharing') ? 0 : 1;
    return route.fallback();
  });
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const input = dialog.getByTestId('sharing-email-input');
  await input.fill('ale');
  await expect(dialog.getByTestId('sharing-email-hint')).toHaveText(
    'Type their full email to share this deck with them.',
  );
  await expect(page.getByRole('listbox')).toHaveCount(0);
  // The access selects' own native options are not suggestions: no element is a custom option.
  await expect(page.locator('[role="option"], [role="combobox"][aria-expanded]')).toHaveCount(0);
  expect(reads).toBe(0);

  await input.fill('ale@team.example');
  await expect(dialog.getByTestId('sharing-email-hint')).toHaveCount(0);
  await input.press('Enter');
  const added = dialog.getByTestId('sharing-person').filter({ hasText: 'ale@team.example' });
  await expect(added.getByTestId('sharing-person-new')).toHaveText('New');
  await expect(added.getByLabel('Access for ale@team.example')).toHaveValue('read');
});

test('SharingDialog_WhenPartialEmailEntered_ShouldNotAdd', async ({ mount, page }) => {
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const input = dialog.getByTestId('sharing-email-input');
  await input.fill('alex');
  await input.press('Enter');
  await dialog.getByTestId('sharing-add-person').click();
  await expect(dialog.getByTestId('sharing-person')).toHaveCount(2);
  await expect(input).toHaveValue('alex');
});

test('SharingDialog_WhenAccessChanged_ShouldSendPeopleWithWrite', async ({ mount, page }) => {
  const puts = await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  await dialog.getByLabel('Access for mira@team.example').selectOption('write');
  await dialog.getByLabel('Access for alex@team.example').selectOption('read');
  await dialog.getByTestId('sharing-save').click();

  await expect(page.getByTestId('saved-level')).toHaveText('named');
  expect(puts).toEqual([
    {
      level: 'named',
      people: [
        { email: 'alex@team.example', access: 'read' },
        { email: 'mira@team.example', access: 'write' },
      ],
    },
  ]);
});

test('SharingDialog_WhenPrivate_ShouldShowPeopleAsPaused', async ({ mount, page }) => {
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  await expect(dialog.getByTestId('sharing-person-paused')).toHaveCount(0);
  await dialog.getByRole('radio', { name: 'Private' }).check();
  await expect(dialog.getByTestId('sharing-person-paused')).toHaveCount(2);
  await expect(dialog.getByLabel('Access for alex@team.example')).toBeEnabled();
  await dialog.getByRole('radio', { name: 'Only people below' }).check();
  await expect(dialog.getByTestId('sharing-person-paused')).toHaveCount(0);
});

test('SharingDialog_WhenNewPeopleAdded_ShouldSayHowManyGetAnEmail', async ({ mount, page }) => {
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const input = dialog.getByTestId('sharing-email-input');
  await expect(dialog.getByTestId('sharing-new-note')).toHaveCount(0);
  await input.fill('kai@team.example');
  await input.press('Enter');
  await expect(dialog.getByTestId('sharing-new-note')).toHaveText('Saving emails 1 new person.');
  await input.fill('lee@team.example');
  await input.press('Enter');
  await expect(dialog.getByTestId('sharing-new-note')).toHaveText('Saving emails 2 new people.');
});

test('SharingDialog_When429_ShouldShowServerMessage', async ({ mount, page }) => {
  const error = 'You can share with 10 new people an hour. Try again in 42 minutes.';
  const puts = await stubSharing(page, sharing, {
    status: 429,
    json: { error },
  });
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const input = dialog.getByTestId('sharing-email-input');
  await input.fill('kai@team.example');
  await input.press('Enter');
  await dialog.getByTestId('sharing-save').click();

  await expect(dialog.getByTestId('sharing-limit-error')).toContainText('Not shared.');
  await expect(dialog.getByTestId('sharing-limit-message')).toHaveText(error);
  await expect(dialog.getByTestId('sharing-limit-error')).toContainText(
    'Someone added to several of your decks counts once. Nothing was saved and no emails went out.',
  );
  await expect(dialog.getByTestId('sharing-person-email')).toHaveText([
    'alex@team.example',
    'mira@team.example',
    'kai@team.example',
  ]);
  await expect(page.getByTestId('saved-level')).toHaveText('none');
  await expect(dialog.getByTestId('sharing-save')).toBeEnabled();
  expect(puts).toHaveLength(1);
});

test('server 400 shows message under the people field', async ({ mount, page }) => {
  const error = 'people: "x@y.z" is not an email address';
  await stubSharing(page, sharing, { status: 400, json: { error } });
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  await dialog.getByTestId('sharing-save').click();

  await expect(dialog.getByTestId('sharing-error')).toHaveText(error);
  await expect(dialog.getByTestId('sharing-error')).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId('saved-level')).toHaveText('none');
  await expect(dialog.getByTestId('sharing-save')).toBeEnabled();
});

test('the owner row shows Owner and has no controls', async ({ mount, page }) => {
  await stubSharing(page, sharing);
  await mount(<SharingDialogHarness deckId={deckId} />);
  await page.getByTestId('open-sharing').click();

  const dialog = page.getByTestId('sharing-dialog');
  const owner = dialog.getByTestId('sharing-owner');
  await expect(owner).toContainText('Sam Rivera (you)');
  await expect(owner).toContainText('sam@team.example');
  await expect(dialog.getByTestId('sharing-owner-label')).toHaveText('Owner');
  await expect(owner.getByRole('combobox')).toHaveCount(0);
  await expect(owner.getByRole('button')).toHaveCount(0);
  // Pair: a person row has both, and an account-less email shows bare.
  const alex = dialog.getByTestId('sharing-person').first();
  await expect(alex.getByTestId('sharing-person-name')).toHaveText('Alex Chen');
  await expect(alex.getByRole('combobox')).toHaveCount(1);
  await expect(alex.getByRole('button')).toHaveCount(1);
  await expect(
    dialog.getByTestId('sharing-person').nth(1).getByTestId('sharing-person-name'),
  ).toHaveCount(0);
});

test('null lastChange hides the line', async ({ mount, page }) => {
  await stubSharing(page, {
    ...sharing,
    level: 'private',
    viewers: [],
    people: [],
    lastChange: null,
  });
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
  await expect(page.getByRole('radio', { name: 'Only people below' })).toBeFocused();

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

  // Forward: the link radio, the people field, Add, each person's access and Remove, Copy link,
  // Cancel, Save, then back to the radio.
  for (const next of [
    'sharing-email-input',
    'sharing-add-person',
    'sharing-person-access',
    'sharing-person-remove',
    'sharing-person-access',
    'sharing-person-remove',
    'sharing-copy-link',
    'sharing-cancel',
    'sharing-save',
  ]) {
    await page.keyboard.press('Tab');
    await expect(dialog.getByTestId(next).and(page.locator(':focus'))).toBeFocused();
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
  await expect(page.getByTestId('sharing-person')).toHaveCount(2);
  await expect(page.getByTestId('sharing-dialog')).toHaveScreenshot('sharing-dialog.png');
});
