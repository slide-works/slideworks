import { test, expect } from '@playwright/experimental-ct-react';
import { templateSharing } from '../test-support/fixtures/templates';
import { stubTemplatesApi } from '../test-support/stubTemplatesApi';
import { TemplateShareDialogHarness } from './testing/TemplateShareDialogHarness';
import { TEMPLATES_TEST_IDS } from './testIds';

const id = templateSharing.link.split('/t/')[1];
const name = 'Northwind kickoff';
const linked = { ...templateSharing, level: 'link' };

const open = async (page: import('playwright/test').Page) => {
  await page.getByTestId('open-template-share').click();
  return page.getByTestId(TEMPLATES_TEST_IDS.shareDialog);
};

test('saves named people and returns focus', async ({ mount, page }) => {
  const viewers = [...templateSharing.viewers, 'sam@team.example'];
  const requests = await stubTemplatesApi(page, {
    setSharing: { json: { ...templateSharing, viewers } },
  });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);

  await expect(dialog.getByRole('heading', { name: `Share "${name}"` })).toBeVisible();
  await expect(dialog.getByRole('radio', { name: 'Named people' })).toBeChecked();
  await expect(dialog.getByRole('radio', { name: 'Named people' })).toBeFocused();
  await expect(dialog.getByTestId('sharing-link')).toHaveCount(0);

  const input = dialog.getByTestId('sharing-email-input');
  await input.fill('sam@team.example');
  await input.press('Enter');
  await expect(dialog.getByTestId('sharing-chip-email')).toHaveText(viewers);
  expect(requests.filter((request) => request.call === 'setSharing')).toEqual([
    { call: 'setSharing', id, body: { level: 'named', viewers } },
  ]);

  await dialog.getByTestId('template-share-done').click();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.shareDialog)).toHaveCount(0);
  await expect(page.getByTestId('open-template-share')).toBeFocused();
});

test('shows the link and copies it', async ({ mount, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await stubTemplatesApi(page, { sharing: { json: linked } });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);

  await expect(dialog.getByRole('radio', { name: 'Anyone with the link' })).toBeChecked();
  await expect(dialog.getByTestId('sharing-link')).toHaveText(templateSharing.link);
  await dialog.getByTestId('sharing-copy-link').click();
  await expect(dialog.getByTestId('template-share-copy-status')).toHaveText('Copied');
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(templateSharing.link);
  await expect(dialog.getByTestId('template-share-note')).toContainText(
    'People you share with can use the template, not edit it.',
  );
});

test('says so when copying fails', async ({ mount, page }) => {
  await stubTemplatesApi(page, { sharing: { json: linked } });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);
  await page.evaluate(() => {
    Object.defineProperty(Navigator.prototype, 'clipboard', {
      get: () => ({ writeText: () => Promise.reject(new Error('refused')) }),
      configurable: true,
    });
  });

  await dialog.getByTestId('sharing-copy-link').click();
  await expect(dialog.getByTestId('template-share-copy-status')).toHaveText(
    'Could not copy. Select the link and copy it.',
  );
});

test('puts the choice back when saving fails', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    setSharing: [
      { status: 400, json: { error: 'sharing_refused', message: 'Sharing is turned off here.' } },
      { status: 500, json: { error: 'Something went wrong. Try again.' } },
    ],
  });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);
  const linkChoice = dialog.getByRole('radio', { name: 'Anyone with the link' });

  await linkChoice.click();
  const alert = dialog.getByTestId('template-share-error');
  await expect(alert).toHaveText('Sharing is turned off here.');
  await expect(dialog.getByRole('radio', { name: 'Named people' })).toBeChecked();
  await expect(linkChoice).toBeFocused();

  await linkChoice.click();
  await expect(alert).toHaveText('Could not change sharing. Try again.');
  await expect(dialog.getByRole('radio', { name: 'Named people' })).toBeChecked();
  await expect(dialog.getByTestId('sharing-chip-email')).toHaveText(templateSharing.viewers);
});

test('changes the level without sending the viewer list', async ({ mount, page }) => {
  const many = Array.from({ length: 51 }, (_, index) => `user${index}@team.example`);
  const requests = await stubTemplatesApi(page, {
    sharing: { json: { ...linked, viewers: many } },
    setSharing: { json: { ...templateSharing, level: 'named', viewers: many } },
  });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);

  await dialog.getByRole('radio', { name: 'Named people' }).check();
  await expect(dialog.getByTestId('sharing-chip')).toHaveCount(51);
  expect(requests.filter((request) => request.call === 'setSharing')).toEqual([
    { call: 'setSharing', id, body: { level: 'named' } },
  ]);
});

test('shows a message when sharing fails to load', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    sharing: [
      { status: 500, json: { error: 'Something went wrong. Try again.' } },
      { json: linked },
    ],
  });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);

  await expect(dialog.getByTestId('template-share-load-error')).toHaveText(
    `Could not load sharing for ${name}.`,
  );
  await dialog.getByTestId('template-share-retry').click();
  await expect(dialog.getByRole('radio', { name: 'Anyone with the link' })).toBeChecked();
});

test('closes on Escape and returns focus', async ({ mount, page }) => {
  await stubTemplatesApi(page);
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);
  await expect(dialog.getByRole('radio', { name: 'Named people' })).toBeFocused();

  await page.keyboard.press('Escape');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.shareDialog)).toHaveCount(0);
  await expect(page.getByTestId('open-template-share')).toBeFocused();
});

test('screenshot: template share dialog on link', async ({ mount, page }) => {
  await stubTemplatesApi(page, { sharing: { json: linked } });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);
  await expect(dialog.getByTestId('sharing-link')).toBeVisible();
  await expect(dialog).toHaveScreenshot('template-share-dialog-link.png');
});

test('closes on Escape after a save failed, and returns focus', async ({ mount, page }) => {
  await stubTemplatesApi(page, { setSharing: { status: 500, json: { error: 'Nope.' } } });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);

  await dialog.getByRole('radio', { name: 'Anyone with the link' }).click();
  await expect(dialog.getByTestId('template-share-error')).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.shareDialog)).toHaveCount(0);
  await expect(page.getByTestId('open-template-share')).toBeFocused();
});

test('closes on Done after a save failed', async ({ mount, page }) => {
  await stubTemplatesApi(page, { setSharing: { status: 500, json: { error: 'Nope.' } } });
  await mount(<TemplateShareDialogHarness templateId={id} name={name} />);
  const dialog = await open(page);

  await dialog.getByRole('radio', { name: 'Anyone with the link' }).click();
  await expect(dialog.getByTestId('template-share-error')).toBeVisible();

  await dialog.getByTestId('template-share-done').click();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.shareDialog)).toHaveCount(0);
});
