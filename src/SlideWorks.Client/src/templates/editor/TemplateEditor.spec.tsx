import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import { LocationProbe } from '../../test-support/LocationProbe';
import { me } from '../../test-support/fixtures/screens';
import { editorTabProps, templateEditor } from '../../test-support/fixtures/templates';
import { stubApi } from '../../test-support/stubApi';
import { stubTemplatesApi } from '../../test-support/stubTemplatesApi';
import { TEMPLATES_TEST_IDS, editorTabButtonTestId, editorTabTestId } from '../testIds';
import type { EditorTab } from '../types';
import { FontsTab } from './tabs/FontsTab';

const path = `/templates/${templateEditor.id}/edit`;
const at = (search = '') => ({ hooksConfig: { route: path + search } });

const TABS: EditorTab[] = ['basics', 'fonts', 'logo-art', 'palette'];

// The one tab that is open: its button is selected and only its content is on the page.
async function expectOpen(page: Page, open: EditorTab) {
  for (const tab of TABS) {
    await expect(page.getByTestId(editorTabButtonTestId(tab))).toHaveAttribute(
      'aria-selected',
      String(tab === open),
    );
    await expect(page.getByTestId(editorTabTestId(tab))).toHaveCount(tab === open ? 1 : 0);
  }
}

test('the tab parameter opens that tab', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page);
  await mount(<AppRoutes />, at('?tab=logo-art'));

  await expect(page.getByRole('heading', { name: 'Northwind kickoff', level: 1 })).toBeVisible();
  await expectOpen(page, 'logo-art');
  await expect(
    page.getByTestId(TEMPLATES_TEST_IDS.editorPanel).getByRole('heading', { name: 'Logo and art' }),
  ).toBeVisible();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.editorPanel)).toHaveAttribute('tabindex', '0');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.preview)).toBeVisible();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.checklist)).toBeVisible();
  expect(requests).toEqual([{ call: 'get', id: templateEditor.id, body: null }]);
});

for (const tab of TABS) {
  test(`?tab=${tab} opens ${tab}`, async ({ mount, page }) => {
    await stubApi(page, { '/api/me': { json: me } });
    await stubTemplatesApi(page);
    await mount(<AppRoutes />, at(`?tab=${tab}`));
    await expectOpen(page, tab);
  });
}

test('no tab parameter opens basics', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, at());
  await expectOpen(page, 'basics');
});

test('a tab parameter that names no tab opens basics', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, at('?tab=nonsense'));
  await expectOpen(page, 'basics');
});

test('the custom layouts tab is not offered yet', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, at('?tab=layouts'));

  await expectOpen(page, 'basics');
  await expect(page.getByRole('tab')).toHaveText(['Basics', 'Fonts', 'Logo and art', 'Palette']);
  await expect(page.getByTestId(editorTabButtonTestId('layouts'))).toHaveCount(0);
  await expect(page.getByTestId(editorTabTestId('layouts'))).toHaveCount(0);
});

test('choosing a tab puts it in the tab parameter and keeps focus on the tab', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    at('?from=list'),
  );
  await expectOpen(page, 'basics');

  await page.getByRole('tab', { name: 'Palette' }).click();

  await expectOpen(page, 'palette');
  await expect(page.getByTestId('location')).toHaveText(`${path}?from=list&tab=palette`);
  await expect(page.getByRole('tab', { name: 'Palette' })).toBeFocused();
});

test('arrow, Home and End keys move along the tab bar and open the tab', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    at('?tab=fonts'),
  );
  await expectOpen(page, 'fonts');
  const tab = (name: string) => page.getByRole('tab', { name, exact: true });

  // Only the open tab is in the Tab order.
  await expect(tab('Fonts')).toHaveAttribute('tabindex', '0');
  for (const other of ['Basics', 'Logo and art', 'Palette'])
    await expect(tab(other)).toHaveAttribute('tabindex', '-1');

  await tab('Fonts').focus();
  const steps: [key: string, open: EditorTab, name: string][] = [
    ['ArrowRight', 'logo-art', 'Logo and art'],
    ['ArrowRight', 'palette', 'Palette'],
    ['ArrowRight', 'basics', 'Basics'],
    ['ArrowLeft', 'palette', 'Palette'],
    ['ArrowLeft', 'logo-art', 'Logo and art'],
    ['Home', 'basics', 'Basics'],
    ['End', 'palette', 'Palette'],
  ];
  for (const [key, open, name] of steps) {
    await page.keyboard.press(key);
    await expectOpen(page, open);
    await expect(tab(name)).toBeFocused();
    await expect(tab(name)).toHaveAttribute('tabindex', '0');
    await expect(page.getByTestId('location')).toHaveText(`${path}?tab=${open}`);
  }
  await expect(page.getByRole('tabpanel')).toHaveAttribute(
    'aria-labelledby',
    'template-editor-tab-palette',
  );
});

test('shows a message when the template cannot be read', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page, { get: { status: 404, json: { error: 'No such template.' } } });
  await mount(<AppRoutes />, at());

  const error = page.getByTestId(TEMPLATES_TEST_IDS.editorError);
  await expect(error).toHaveText('This template is not here, or it is not yours.');
  await expect(error).toHaveAttribute('role', 'alert');
  await expect(page.getByRole('link', { name: 'Back to your templates' })).toHaveAttribute(
    'href',
    '/templates',
  );
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.editorTabs)).toHaveCount(0);
});

test('a tab mounts alone with props built from the fixture', async ({ mount, page }) => {
  await mount(<FontsTab {...editorTabProps()} />);

  await expect(page.getByTestId(editorTabTestId('fonts'))).toBeVisible();
  await expect(page.getByRole('region', { name: 'Fonts of Northwind kickoff' })).toBeVisible();
});

const savedWith = (manifest: Partial<typeof templateEditor.manifest>) => ({
  ...templateEditor,
  revision: templateEditor.revision + 1,
  manifest: { ...templateEditor.manifest, ...manifest },
});

const refusal = (status: number, error: string, message: string, path?: string) => ({
  status,
  json: { error, message, ...(path === undefined ? {} : { path }), errors: [] },
});

test('shows a loading message first', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, at());
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.editor)).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Basics' })).toBeVisible();
  await expect(page.getByText('Loading the template…')).toHaveCount(0);
});

test('shows the draft state and the save state in the header', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, at());
  await expect(page.getByTestId('template-editor-status')).toHaveText('Published · v2');
  await expect(page.getByTestId('template-editor-save-state')).toHaveText('Saved');
  await expect(page.getByRole('link', { name: 'Download bundle' })).toHaveAttribute(
    'href',
    `/api/templates/${templateEditor.id}/bundle`,
  );
});

test('saves a changed field and keeps focus', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page, {
    save: [
      { json: savedWith({ chrome: { ...templateEditor.manifest.chrome, tracker: false } }) },
      {
        json: {
          ...savedWith({ purpose: 'A new purpose.' }),
          revision: 9,
          manifest: {
            ...templateEditor.manifest,
            chrome: { ...templateEditor.manifest.chrome, tracker: false },
            purpose: 'A new purpose.',
          },
        },
      },
    ],
  });
  await mount(<AppRoutes />, at());

  // A tick saves at once, and focus stays on it.
  const tracker = page.getByRole('checkbox', { name: 'Part tracker' });
  await tracker.focus();
  await tracker.press('Space');
  await expect.poll(() => requests.filter(({ call }) => call === 'save').length).toBe(1);
  await expect(page.getByTestId('template-editor-save-state')).toHaveText('Saved');
  await expect(tracker).toBeFocused();
  await expect(tracker).not.toBeChecked();
  expect(requests[1].body).toEqual({
    manifest: {
      ...templateEditor.manifest,
      chrome: { ...templateEditor.manifest.chrome, tracker: false },
    },
    revision: 7,
  });

  // Text saves when it loses focus, with the revision the last save answered.
  const purpose = page.getByLabel('What it is for');
  await purpose.fill('A new purpose.');
  expect(requests).toHaveLength(2);
  await purpose.press('Tab');
  await expect.poll(() => requests.filter(({ call }) => call === 'save').length).toBe(2);
  expect(requests[2].body).toMatchObject({
    manifest: { purpose: 'A new purpose.' },
    revision: 8,
  });
  await expect(purpose).toHaveValue('A new purpose.');
  await expect(page.getByTestId('template-editor-save-state')).toHaveText('Saved');
  await expect(page.getByTestId('template-layout-chip-title')).toBeFocused();
});

test('does not save a text field that was not changed', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page);
  await mount(<AppRoutes />, at());
  const purpose = page.getByLabel('What it is for');
  await purpose.focus();
  await purpose.press('Tab');
  await expect(page.getByText('join and poll are always allowed.')).toBeVisible();
  expect(requests.map(({ call }) => call)).toEqual(['get']);
});

test('saves a layout chip and a shape choice', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const { chrome, shape } = templateEditor.manifest;
  const withChart = [...templateEditor.manifest.layouts, 'chart'];
  const requests = await stubTemplatesApi(page, {
    save: [
      { json: savedWith({ layouts: withChart }) },
      {
        json: {
          ...savedWith({
            layouts: withChart,
            chrome,
            shape: { ...shape, radius: { sm: 14, md: 16, lg: 20, xl: 22 } },
          }),
          revision: 9,
        },
      },
    ],
  });
  await mount(<AppRoutes />, at());
  const chip = page.getByRole('button', { name: 'chart', exact: true });
  await expect(chip).toHaveAttribute('aria-pressed', 'false');
  await chip.click();
  await expect(chip).toHaveAttribute('aria-pressed', 'true');
  await expect(chip).toBeFocused();
  await page.getByRole('radio', { name: '16 px' }).check();
  await expect.poll(() => requests.filter(({ call }) => call === 'save').length).toBe(2);
  const [, first, second] = requests;
  expect((first.body as { manifest: { layouts: string[] } }).manifest.layouts).toContain('chart');
  expect(
    (second.body as { manifest: { shape: { radius: { md: number } } } }).manifest.shape.radius,
  ).toEqual({ sm: 14, md: 16, lg: 20, xl: 22 });
});

test('shows a message when saving fails', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page, {
    save: [{ status: 500, json: { error: 'boom' } }, { json: savedWith({ purpose: 'Kept.' }) }],
  });
  await mount(<AppRoutes />, at());
  const purpose = page.getByLabel('What it is for');
  await purpose.fill('Kept.');
  await purpose.press('Tab');

  const alert = page.getByTestId(TEMPLATES_TEST_IDS.editorSaveError);
  await expect(alert).toHaveAttribute('role', 'alert');
  await expect(alert).toContainText('Could not save. Your change is still here.');
  await expect(purpose).toHaveValue('Kept.');
  await expect(page.getByTestId('template-editor-save-state')).toHaveText('Not saved');

  await alert.getByRole('button', { name: 'Retry' }).click();
  await expect(alert).toHaveCount(0);
  await expect(purpose).toHaveValue('Kept.');
  expect(requests.filter(({ call }) => call === 'save')).toHaveLength(2);
});

test('marks the field the server refused', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const message = 'The short name must be lower case, digits and hyphens.';
  await stubTemplatesApi(page, { save: refusal(400, 'manifest_invalid', message, 'name') });
  await mount(<AppRoutes />, at());
  const short = page.getByLabel('Short name');
  await short.fill('Not Valid');
  await short.press('Tab');
  await page.getByLabel('What it is for').focus();
  await short.fill('Not Valid');
  await page.getByLabel('Name', { exact: true }).focus();
  await page.getByLabel('Name', { exact: true }).press('Tab');

  await expect(short).toHaveAttribute('aria-invalid', 'true');
  await expect(short).toBeFocused();
  await expect(short).toHaveValue('Not Valid');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.editorSaveError)).toHaveText(message);
  await expect(page.getByLabel('Name', { exact: true })).not.toHaveAttribute(
    'aria-invalid',
    'true',
  );
});

test('stops saving after a stale revision', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page, {
    save: refusal(409, 'stale_revision', 'Read it again.'),
  });
  await mount(<AppRoutes />, at());
  const tracker = page.getByRole('checkbox', { name: 'Part tracker' });
  await tracker.click();

  const alert = page.getByTestId(TEMPLATES_TEST_IDS.editorSaveError);
  await expect(alert).toContainText(
    'Someone else changed this template. Reload to see their changes.',
  );
  await expect(tracker).toBeChecked();
  const saves = () => requests.filter(({ call }) => call === 'save').length;
  expect(saves()).toBe(1);

  // Nothing more is sent until Reload.
  await page.getByRole('checkbox', { name: 'Progress bar' }).click();
  await expect(alert).toContainText('Someone else changed this template.');
  expect(saves()).toBe(1);

  await alert.getByRole('button', { name: 'Reload' }).click();
  await expect(alert).toHaveCount(0);
  await page.getByRole('checkbox', { name: 'Progress bar' }).click();
  await expect.poll(saves).toBe(2);
});

const firstFrameRadius = (page: Page) =>
  page
    .getByTestId('look-preview-slide')
    .first()
    .evaluate((el) => getComputedStyle(el).getPropertyValue('--sw-radius-md').trim());

test('a refused tick and a refused shape go back to the saved value in the tab and the preview', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page, {
    save: refusal(400, 'asset_not_found', 'No such file.', 'fonts[0].file'),
  });
  await mount(<AppRoutes />, at());
  const tracker = page.getByRole('checkbox', { name: 'Part tracker' });
  const was = await tracker.isChecked();
  await tracker.click();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.editorSaveError)).toHaveText('No such file.');
  await expect(tracker).toBeChecked({ checked: was });

  const before = await firstFrameRadius(page);
  await page.getByRole('radio', { name: '16 px' }).click();
  await expect.poll(() => requests.filter(({ call }) => call === 'save').length).toBe(2);
  await expect(page.getByRole('radio', { name: '16 px' })).not.toBeChecked();
  await expect.poll(() => firstFrameRadius(page)).toBe(before);

  // The next change does not send the refused tick again.
  await page.getByRole('checkbox', { name: 'Progress bar' }).click();
  await expect.poll(() => requests.filter(({ call }) => call === 'save').length).toBe(3);
  const sent = (requests[3].body as { manifest: typeof templateEditor.manifest }).manifest;
  expect(sent.chrome.tracker).toBe(was);
});

test('a change that failed is sent again with the next one, and the alert stays until then', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me } });
  const chrome = templateEditor.manifest.chrome;
  const merged = savedWith({
    chrome: { ...chrome, tracker: !chrome.tracker },
    purpose: 'Both.',
  });
  const requests = await stubTemplatesApi(page, {
    save: ['network-down', { json: merged }],
  });
  await mount(<AppRoutes />, at());
  const alert = page.getByTestId(TEMPLATES_TEST_IDS.editorSaveError);
  await page.getByRole('checkbox', { name: 'Part tracker' }).click();
  await expect(alert).toContainText('Could not save. Your change is still here.');

  const purpose = page.getByLabel('What it is for');
  await purpose.fill('Both.');
  await purpose.press('Tab');
  await expect.poll(() => requests.filter(({ call }) => call === 'save').length).toBe(2);
  await expect(alert).toHaveCount(0);
  await expect(page.getByTestId('template-editor-save-state')).toHaveText('Saved');
  const body = requests[2].body as { manifest: typeof templateEditor.manifest };
  expect(body.manifest.chrome.tracker).toBe(!chrome.tracker);
  expect(body.manifest.purpose).toBe('Both.');
});

test('a refused text field is not sent again until it changes, and focus can leave it', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page, {
    save: refusal(400, 'manifest_invalid', 'Not valid.', 'name'),
  });
  await mount(<AppRoutes />, at());
  const short = page.getByLabel('Short name');
  await short.fill('Not Valid');
  await short.press('Tab');
  await expect(short).toBeFocused();
  const saves = () => requests.filter(({ call }) => call === 'save').length;
  expect(saves()).toBe(1);

  await short.press('Tab');
  await expect(page.getByLabel('What it is for')).toBeFocused();
  await page.getByLabel('What it is for').press('Tab');
  await expect(page.getByTestId('template-layout-chip-title')).toBeFocused();
  expect(saves()).toBe(1);
});

test('the tab bar ignores a key pressed with Alt', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, at('?tab=fonts'));
  await page.getByRole('tab', { name: 'Fonts' }).focus();
  await page.keyboard.press('Alt+ArrowLeft');
  await expectOpen(page, 'fonts');
});

test('screenshot: editor on the basics tab', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, at());
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.preview)).toBeVisible();
  await expect(page).toHaveScreenshot('template-editor-basics.png', { maxDiffPixels: 200 });
});
