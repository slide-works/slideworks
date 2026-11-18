import { test, expect } from '@playwright/experimental-ct-react';
import { templates } from '../test-support/fixtures/templates';
import { LocationProbe } from '../test-support/LocationProbe';
import { stubTemplatesApi } from '../test-support/stubTemplatesApi';
import { TemplatesPage } from './TemplatesPage';
import { TEMPLATES_TEST_IDS } from './testIds';

const route = { hooksConfig: { route: '/templates' } };
const [builtIn] = templates.builtIn;
const page_ = () => (
  <LocationProbe>
    <TemplatesPage />
  </LocationProbe>
);

test('starts from a built-in', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page, {
    create: { status: 201, json: { id: 'tpl_new000000001' } },
  });
  await mount(page_(), route);

  await page.getByRole('button', { name: `Start from ${builtIn.name}` }).click();
  const dialog = page.getByTestId(TEMPLATES_TEST_IDS.newDialog);
  await expect(dialog.getByRole('radio', { name: new RegExp(builtIn.name) })).toBeChecked();
  await expect(dialog).toContainText(builtIn.purpose);
  await dialog.getByLabel('Name').fill('Team offsite');
  await dialog.getByRole('button', { name: 'Create draft' }).click();

  await expect(page.getByTestId('location')).toHaveText('/templates/tpl_new000000001/edit');
  expect(requests.filter((r) => r.call === 'create')).toEqual([
    { call: 'create', id: null, body: { basedOn: builtIn.id, name: 'Team offsite' } },
  ]);
});

test('fills the editor from a bundle', async ({ mount, page }) => {
  await stubTemplatesApi(page);
  await page.route('**/api/templates/import', (route) =>
    route.fulfill({ status: 201, json: { id: 'tpl_bundle000001' } }),
  );
  await mount(page_(), route);

  await page.getByLabel('Template bundle').setInputFiles({
    name: 'kit.zip',
    mimeType: 'application/zip',
    buffer: Buffer.from('PK'),
  });
  await expect(page.getByTestId('location')).toHaveText('/templates/tpl_bundle000001/edit');
});

test('keeps the dialog open when creating fails', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    create: [
      {
        status: 400,
        json: { error: 'name_taken', message: 'You already have a template with that name.' },
      },
      { status: 500, json: {} },
    ],
  });
  await mount(page_(), route);

  await page.getByRole('button', { name: 'New template' }).click();
  const dialog = page.getByTestId(TEMPLATES_TEST_IDS.newDialog);
  const name = dialog.getByLabel('Name');
  await name.fill('Quarterly review');
  await dialog.getByRole('button', { name: 'Create draft' }).click();

  await expect(dialog.getByRole('alert')).toHaveText('You already have a template with that name.');
  await expect(name).toBeFocused();
  await expect(name).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByTestId('location')).toHaveText('/templates');

  await dialog.getByRole('button', { name: 'Create draft' }).click();
  await expect(dialog.getByRole('alert')).toHaveText('Could not create the template. Try again.');
  await expect(dialog).toBeVisible();
});

test('focuses the name and returns focus on cancel', async ({ mount, page }) => {
  await stubTemplatesApi(page);
  await mount(page_(), route);

  const open = page.getByRole('button', { name: 'New template' }).first();
  await open.click();
  const dialog = page.getByTestId(TEMPLATES_TEST_IDS.newDialog);
  await expect(dialog.getByLabel('Name')).toBeFocused();
  await expect(dialog.getByLabel('Name')).toHaveAttribute('maxlength', '60');
  await expect(dialog.getByRole('radio').first()).toBeChecked();

  await dialog.getByRole('button', { name: 'Cancel' }).click();
  await expect(dialog).toHaveCount(0);
  await expect(open).toBeFocused();

  await open.click();
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await expect(open).toBeFocused();
});

test('screenshot: new template dialog', async ({ mount, page }) => {
  await stubTemplatesApi(page);
  await mount(page_(), route);
  await page.getByRole('button', { name: `Start from ${builtIn.name}` }).click();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.newDialog)).toHaveScreenshot(
    'new-template-dialog.png',
  );
});
