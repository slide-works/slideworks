import { test, expect } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright/test';
import { AppRoutes } from '../../../AppRoutes';
import { me } from '../../../test-support/fixtures/screens';
import { layoutReview, templateEditor } from '../../../test-support/fixtures/templates';
import { stubApi } from '../../../test-support/stubApi';
import {
  stubTemplateAssets,
  type AssetStubOptions,
} from '../../../test-support/stubTemplateAssets';
import { stubTemplatesApi } from '../../../test-support/stubTemplatesApi';
import { editorTabTestId } from '../../testIds';
import type { TemplateEditorView, TemplateManifest } from '../../types';

const id = templateEditor.id;
const route = { hooksConfig: { route: `/templates/${id}/edit?tab=layouts` } };

const slotsJson = JSON.stringify({ name: 'bar-chart', surface: 'light', slots: {} });
const files = (json = slotsJson) => [
  { name: 'layout.html', mimeType: 'text/html', buffer: Buffer.from('<div class="chart"></div>') },
  { name: 'layout.css', mimeType: 'text/css', buffer: Buffer.from('.chart { color: red; }') },
  { name: 'slots.json', mimeType: 'application/json', buffer: Buffer.from(json) },
];

const view = (customLayouts: TemplateManifest['customLayouts']): TemplateEditorView => ({
  ...templateEditor,
  revision: templateEditor.revision + 1,
  manifest: { ...templateEditor.manifest, customLayouts },
});
const withLayout = (name: string, file: string) => view([{ name, file }]);
const draftWith = (customLayouts: TemplateManifest['customLayouts']) => ({
  json: { ...templateEditor, manifest: { ...templateEditor.manifest, customLayouts } },
});
const sentManifest = (body: unknown) => (body as { manifest: TemplateManifest }).manifest;

async function open(
  page: Page,
  mount: (component: React.JSX.Element, options: typeof route) => Promise<Locator>,
  responses: Parameters<typeof stubTemplatesApi>[1] = {},
  assets: AssetStubOptions = {},
) {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page, responses);
  const log = await stubTemplateAssets(page, id, assets);
  await mount(<AppRoutes />, route);
  const tab = page.getByTestId(editorTabTestId('layouts'));
  await expect(tab.getByRole('heading', { name: 'Custom layouts', level: 2 })).toBeVisible();
  return { tab, requests, log, picker: tab.locator('input[type=file]') };
}

const refusal = (errors: { path: string; code: string; message: string }[]) => ({
  put: () => ({
    status: 400,
    json: { error: 'layout_refused', message: 'The layout was not accepted.', errors },
  }),
});

test('upload shows the sanitiser’s refusals', async ({ mount, page }) => {
  const { tab, picker, log } = await open(
    page,
    mount,
    {},
    refusal([
      { path: 'layout.html:12', code: 'markup_tag', message: 'script is not an allowed tag.' },
      { path: 'slots.lanes.of.progress', code: 'slots_type', message: 'progress has no type.' },
    ]),
  );
  await picker.setInputFiles(files());

  const alert = tab.getByRole('alert');
  const heading = alert.getByRole('heading', { name: 'bar-chart was not accepted' });
  await expect(heading).toBeVisible();
  await expect(heading).toBeFocused();
  await expect(alert.getByText('layout.html:12')).toBeVisible();
  await expect(alert.getByText('script is not an allowed tag.')).toBeVisible();
  await expect(alert.getByText('slots.lanes.of.progress')).toBeVisible();
  await expect(alert.getByRole('listitem')).toHaveCount(2);
  expect(log.filter((line) => line.startsWith('start layout'))).toEqual([
    'start layout bar-chart.json rights=false',
  ]);
});

test('a refusal is drawn as text', async ({ mount, page }) => {
  const { tab, picker } = await open(
    page,
    mount,
    {},
    refusal([{ path: 'layout.html:3', code: 'markup_tag', message: '<script>alert(1)</script>' }]),
  );
  await picker.setInputFiles(files());

  await expect(tab.getByText('<script>alert(1)</script>')).toBeVisible();
  await expect(tab.locator('script')).toHaveCount(0);
});

test('an accepted upload saves the draft with the layout', async ({ mount, page }) => {
  const saved = withLayout('bar-chart', 'new-tok0');
  const { tab, picker, requests, log } = await open(page, mount, {
    save: { json: saved },
    layoutReview: { json: layoutReview },
  });
  await picker.setInputFiles(files());

  const link = tab.getByRole('link', { name: 'Review bar-chart' });
  await expect(link).toBeFocused();
  await expect(link).toHaveAttribute('href', `/templates/${id}/layouts/bar-chart`);
  const save = requests.find(({ call }) => call === 'save');
  expect(sentManifest(save?.body).customLayouts).toEqual([{ name: 'bar-chart', file: 'new-tok0' }]);
  // The upload is one JSON document built from the three files.
  expect(log.filter((line) => line.startsWith('put'))).toHaveLength(1);
});

test('replacing the files returns the layout to Needs review', async ({ mount, page }) => {
  const before = [{ name: 'bar-chart', file: layoutReview.asset }];
  const { tab, picker, requests } = await open(page, mount, {
    get: draftWith(before),
    save: { json: withLayout('bar-chart', 'new-tok0') },
    layoutReview: [
      { json: { ...layoutReview, approved: true } },
      { json: { ...layoutReview, approved: false } },
    ],
  });
  const row = tab.getByTestId('custom-layout-row');
  await expect(row.getByTestId('custom-layout-status')).toHaveText('Approved');

  await picker.setInputFiles(files());

  await expect(row.getByTestId('custom-layout-status')).toHaveText('Needs review');
  const save = requests.find(({ call }) => call === 'save');
  expect(sentManifest(save?.body).customLayouts).toEqual([{ name: 'bar-chart', file: 'new-tok0' }]);
});

test('the tab lists each layout with its status', async ({ mount, page }) => {
  const { tab } = await open(page, mount, {
    get: draftWith([
      { name: 'bar-chart', file: layoutReview.asset },
      { name: 'funnel', file: 'f2' },
    ]),
    layoutReview: [
      { json: { ...layoutReview, approved: true } },
      { json: { ...layoutReview, name: 'funnel', approved: false } },
    ],
  });

  const rows = tab.getByTestId('custom-layout-row');
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toContainText('bar-chart');
  await expect(rows.nth(0).getByTestId('custom-layout-status')).toHaveText('Approved');
  await expect(rows.nth(1)).toContainText('funnel');
  await expect(rows.nth(1).getByTestId('custom-layout-status')).toHaveText('Needs review');
  await expect(rows.nth(1).getByRole('link', { name: 'Review funnel' })).toHaveAttribute(
    'href',
    `/templates/${id}/layouts/funnel`,
  );
});

test('wrong files are refused before any call', async ({ mount, page }) => {
  const { tab, picker, log, requests } = await open(page, mount);
  const [html, css] = files();
  await picker.setInputFiles([html, css]);

  await expect(tab.getByText('Choose one .html, one .css and one .json file.')).toBeVisible();
  expect(log).toEqual([]);
  expect(requests.map(({ call }) => call)).toEqual(['get']);
});

test('a .json that does not parse is refused before any call', async ({ mount, page }) => {
  const { tab, picker, log } = await open(page, mount);
  await picker.setInputFiles(files('{ not json'));

  const heading = tab.getByRole('heading', { name: 'slots.json was not accepted' });
  await expect(heading).toBeFocused();
  await expect(tab.getByText('This file is not valid JSON.')).toBeVisible();
  expect(log).toEqual([]);
});

test('a network failure says so and saves nothing', async ({ mount, page }) => {
  const { tab, picker, requests } = await open(page, mount, {}, { start: () => 'network-down' });
  await picker.setInputFiles(files());

  await expect(tab.getByText('Could not upload the layout. Try again.')).toBeVisible();
  expect(requests.map(({ call }) => call)).toEqual(['get']);
});

test('remove saves the draft without the layout', async ({ mount, page }) => {
  const { tab, requests } = await open(page, mount, {
    get: draftWith([{ name: 'bar-chart', file: layoutReview.asset }]),
    save: { json: view([]) },
  });
  await tab.getByRole('button', { name: 'Remove layout bar-chart' }).click();

  await expect(tab.getByText('No custom layouts yet.')).toBeVisible();
  await expect(tab.getByRole('button', { name: 'Upload a custom layout' })).toBeFocused();
  const save = requests.find(({ call }) => call === 'save');
  expect(sentManifest(save?.body).customLayouts).toEqual([]);
});

test('a failed remove says so and keeps the layout', async ({ mount, page }) => {
  const { tab } = await open(page, mount, {
    get: draftWith([{ name: 'bar-chart', file: layoutReview.asset }]),
    save: { status: 500 },
  });
  await tab.getByRole('button', { name: 'Remove layout bar-chart' }).click();

  await expect(tab.getByText('Could not remove bar-chart. Try again.')).toBeVisible();
  await expect(tab.getByTestId('custom-layout-row')).toHaveCount(1);
});
