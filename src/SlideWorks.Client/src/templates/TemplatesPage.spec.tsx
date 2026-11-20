import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { templates } from '../test-support/fixtures/templates';
import { LocationProbe } from '../test-support/LocationProbe';
import { stubTemplatesApi } from '../test-support/stubTemplatesApi';
import { TemplatesPage } from './TemplatesPage';
import { TEMPLATES_TEST_IDS } from './testIds';

const route = { hooksConfig: { route: '/templates' } };
const [mine, draft] = templates.mine;
const emptyMine = { ...templates, mine: [] };
const page_ = () => (
  <LocationProbe>
    <TemplatesPage />
  </LocationProbe>
);

const rowsOf = (page: Page, group: string) =>
  page.getByRole('rowgroup', { name: group }).getByTestId(TEMPLATES_TEST_IDS.row);

const zip = { name: 'kit.zip', mimeType: 'application/zip', buffer: Buffer.from('PK-zip-bytes') };

test('lists mine, shared and built-in', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page);
  await mount(page_(), route);

  await expect(page.getByRole('heading', { name: 'Templates', level: 1 })).toBeVisible();
  await expect(rowsOf(page, 'Yours').getByTestId(TEMPLATES_TEST_IDS.rowName)).toHaveText(
    templates.mine.map((row) => row.name),
  );
  const yours = rowsOf(page, 'Yours').first();
  await expect(yours).toContainText('northwind-kickoff');
  await expect(yours).toContainText('Northwind Sans / Northwind Text');
  await expect(yours).toContainText('Northwind · locked');
  await expect(yours).toContainText('Named people');
  await expect(yours).toContainText('Published · v2');
  await expect(yours.getByRole('link', { name: 'Edit Northwind kickoff' })).toHaveAttribute(
    'href',
    `/templates/${mine.id}/edit`,
  );
  await expect(rowsOf(page, 'Yours').nth(1)).toContainText('Draft');
  await expect(rowsOf(page, 'Yours').nth(1)).toContainText('any');

  const shared = rowsOf(page, 'Shared with you');
  await expect(shared.getByTestId(TEMPLATES_TEST_IDS.rowName)).toHaveText(['Harbour talks']);
  await expect(shared).toContainText('by Mira Okafor');
  await expect(shared.getByRole('link', { name: 'View Harbour talks' })).toHaveAttribute(
    'href',
    `/t/${templates.shared[0].id}`,
  );

  const builtIn = rowsOf(page, 'Built-in');
  await expect(builtIn.getByTestId(TEMPLATES_TEST_IDS.rowName)).toHaveText(['Workshop']);
  await expect(builtIn).toContainText('Built-in');
  await expect(page.getByTestId('templates-empty')).toHaveCount(0);
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.error)).toHaveCount(0);
  expect(requests).toEqual([{ call: 'list', id: null, body: null }]);
});

test('shows the empty state', async ({ mount, page }) => {
  await stubTemplatesApi(page, { list: { json: emptyMine } });
  await mount(page_(), route);

  const empty = page.getByTestId('templates-empty');
  await expect(empty).toContainText('No templates of your own yet');
  await expect(empty).toContainText('Make one to give your decks your fonts, logo and colours.');
  await empty.getByRole('button', { name: 'New template' }).click();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.newDialog)).toBeVisible();
  await expect(page.getByRole('rowgroup', { name: 'Yours' })).toHaveCount(0);
  await expect(rowsOf(page, 'Built-in')).toHaveCount(1);
});

test('shows a message when the list fails and retries', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    list: [
      { status: 500, json: { error: 'Something went wrong. Try again.' } },
      { json: templates },
    ],
  });
  await mount(page_(), route);

  const error = page.getByTestId(TEMPLATES_TEST_IDS.error);
  await expect(error).toHaveText('Something went wrong. Try again.');
  await expect(error).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.row)).toHaveCount(0);
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.row)).toHaveCount(4);
  await expect(error).toHaveCount(0);
});

test('shows a message when the list fails', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    list: { status: 500, json: { error: 'Something went wrong. Try again.' } },
  });
  await mount(page_(), route);

  const error = page.getByTestId(TEMPLATES_TEST_IDS.error);
  await expect(error).toHaveText('Something went wrong. Try again.');
  await expect(error).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.row)).toHaveCount(0);
});

test('shows a message when the server cannot be reached', async ({ mount, page }) => {
  await stubTemplatesApi(page, { list: 'network-down' });
  await mount(page_(), route);

  await expect(page.getByTestId(TEMPLATES_TEST_IDS.error)).toHaveText("Can't reach SlideWorks.");
});

test('archive asks first and reports a failure', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page, {
    archive: [
      { status: 404, json: { error: 'No template.', message: 'That template is gone.' } },
      { status: 204 },
    ],
  });
  await mount(page_(), route);

  await page.getByRole('button', { name: `Archive ${mine.name}` }).click();
  const dialog = page.getByRole('dialog', { name: `Archive "${mine.name}"?` });
  await expect(dialog).toContainText('3 decks keep this look. Nobody can pick it for a new deck.');
  expect(requests.filter((r) => r.call === 'archive')).toEqual([]);

  await dialog.getByRole('button', { name: 'Archive' }).click();
  await expect(dialog.getByRole('alert')).toHaveText('That template is gone.');
  await expect(dialog).toBeVisible();
  expect(requests.filter((r) => r.call === 'archive')).toEqual([
    { call: 'archive', id: mine.id, body: null },
  ]);

  await dialog.getByRole('button', { name: 'Archive' }).click();
  await expect(dialog).toHaveCount(0);
  expect(requests.filter((r) => r.call === 'list')).toHaveLength(2);
});

test('archive says so itself when the server gave no sentence', async ({ mount, page }) => {
  await stubTemplatesApi(page, { archive: { status: 500, json: {} } });
  await mount(page_(), route);

  await page.getByRole('button', { name: `Archive ${draft.name}` }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Archive' }).click();
  await expect(dialog.getByRole('alert')).toHaveText(`Could not archive ${draft.name}. Try again.`);
});

test('archive returns focus to the row', async ({ mount, page }) => {
  const archivedList = {
    ...templates,
    mine: [{ ...mine, status: 'archived' as const }, draft],
  };
  await stubTemplatesApi(page, { list: [{ json: templates }, { json: archivedList }] });
  await mount(page_(), route);

  const button = page.getByRole('button', { name: `Archive ${draft.name}` });
  await button.click();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(button).toBeFocused();

  await button.click();
  await page.getByRole('dialog').getByRole('button', { name: 'Cancel' }).click();
  await expect(button).toBeFocused();

  const gone = page.getByRole('button', { name: `Archive ${mine.name}` });
  await gone.click();
  await page.getByRole('dialog').getByRole('button', { name: 'Archive' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(gone).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Templates', level: 1 })).toBeFocused();
});

test('uploads a bundle and opens the editor', async ({ mount, page }) => {
  await stubTemplatesApi(page);
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => (release = resolve));
  const seen: { type: string | undefined; size: number }[] = [];
  await page.route('**/api/templates/import', async (route) => {
    const request = route.request();
    seen.push({
      type: request.headers()['content-type'],
      size: request.postDataBuffer()?.length ?? 0,
    });
    await held;
    await route.fulfill({ status: 201, json: { id: 'tpl_imported0001' } });
  });
  await mount(page_(), route);

  await page.getByLabel('Template bundle').setInputFiles(zip);
  const button = page.getByRole('button', { name: 'Uploading…' });
  await expect(button).toBeDisabled();
  release();
  await expect(page.getByTestId('location')).toHaveText('/templates/tpl_imported0001/edit');
  expect(seen).toEqual([{ type: 'application/zip', size: zip.buffer.length }]);
});

test("shows the server's refusal for a bad bundle", async ({ mount, page }) => {
  await stubTemplatesApi(page);
  const answers = [
    {
      status: 400,
      json: { error: 'template_json_missing', message: 'The bundle has no template.json.' },
    },
    { status: 500, json: {} },
  ];
  await page.route('**/api/templates/import', (route) => route.fulfill(answers.shift() ?? {}));
  await mount(page_(), route);

  const upload = page.getByRole('button', { name: 'Upload a bundle…' });
  await page.getByLabel('Template bundle').setInputFiles(zip);
  await expect(page.getByRole('alert').filter({ hasText: 'template.json' })).toHaveText(
    'The bundle has no template.json.',
  );
  await expect(upload).toBeFocused();
  await expect(page.getByTestId('location')).toHaveText('/templates');

  await page.getByLabel('Template bundle').setInputFiles(zip);
  await expect(page.getByRole('alert').filter({ hasText: 'bundle' })).toHaveText(
    'Could not read that bundle.',
  );
  await expect(upload).toBeFocused();
});

test('downloads a template of mine as a bundle', async ({ mount, page }) => {
  await stubTemplatesApi(page);
  await mount(page_(), route);

  const link = page.getByRole('link', { name: `Download ${mine.name} as a bundle` });
  await expect(link).toHaveAttribute('href', `/api/templates/${mine.id}/bundle`);
  await expect(link).toHaveAttribute('download', '');
});

test('screenshot: templates page', async ({ mount, page }) => {
  await stubTemplatesApi(page);
  await mount(page_(), route);
  await expect(rowsOf(page, 'Built-in')).toHaveCount(1);
  await expect(page).toHaveScreenshot('templates-page.png');
});
