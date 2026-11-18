import { test, expect } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright/test';
import { AppRoutes } from '../../../AppRoutes';
import { me } from '../../../test-support/fixtures/screens';
import { templateEditor } from '../../../test-support/fixtures/templates';
import { stubApi } from '../../../test-support/stubApi';
import { stubTemplateAssets, storedAsset } from '../../../test-support/stubTemplateAssets';
import { stubTemplatesApi } from '../../../test-support/stubTemplatesApi';
import { editorTabTestId } from '../../testIds';
import type { TemplateAsset, TemplateEditorView, TemplateManifest } from '../../types';

const id = templateEditor.id;
const route = { hooksConfig: { route: `/templates/${id}/edit?tab=fonts` } };
const woff2 = (name: string) => ({ name, mimeType: 'font/woff2', buffer: Buffer.from([1, 2, 3]) });

const view = (
  manifest: Partial<TemplateManifest>,
  assets: TemplateAsset[] = templateEditor.assets,
): TemplateEditorView => ({
  ...templateEditor,
  manifest: { ...templateEditor.manifest, ...manifest },
  assets,
});

const notFont = templateEditor.assets.filter((asset) => asset.kind !== 'font');
const noFonts = view({ fonts: [] }, notFont);
const confirmed = (asset: TemplateAsset): TemplateAsset => ({ ...asset, rightsConfirmed: true });
const sentManifest = (body: unknown) => (body as { manifest: TemplateManifest }).manifest;

async function open(
  page: Page,
  mount: (component: React.JSX.Element, options: typeof route) => Promise<Locator>,
  gets: TemplateEditorView[],
  extra: Parameters<typeof stubTemplatesApi>[1] = {},
  assets: Parameters<typeof stubTemplateAssets>[2] = {},
) {
  await stubApi(page, { '/api/me': { json: me } });
  const requests = await stubTemplatesApi(page, {
    get: gets.map((json) => ({ json })),
    ...extra,
  });
  const log = await stubTemplateAssets(page, id, assets);
  await mount(<AppRoutes />, route);
  const tab = page.getByTestId(editorTabTestId('fonts'));
  await expect(tab.getByRole('heading', { name: 'Fonts', level: 2 })).toBeVisible();
  return { tab, requests, log };
}

test('keeps the button off until the rights tick', async ({ mount, page }) => {
  const { tab, log } = await open(page, mount, [noFonts]);
  const add = tab.getByRole('button', { name: 'Add font files' });
  const tick = tab.getByRole('checkbox', {
    name: 'I have the right to serve these fonts on the web',
  });

  await expect(tick).not.toBeChecked();
  await expect(add).toBeDisabled();
  await expect(add).toHaveAccessibleDescription(/Tick the box above first/);

  await tick.check();
  await expect(add).toBeEnabled();
  await expect(tick).toBeChecked();
  await expect(tick).toBeDisabled();
  expect(log).toEqual(['rights']);
});

test('confirms the rights for files already there', async ({ mount, page }) => {
  const { tab, log } = await open(page, mount, [
    templateEditor,
    view({}, templateEditor.assets.map(confirmed)),
  ]);
  const tick = tab.getByRole('checkbox', {
    name: 'I have the right to serve these fonts on the web',
  });
  await expect(tick).not.toBeChecked();
  await expect(tab.getByRole('button', { name: 'Add font files' })).toBeDisabled();

  await tick.check();
  await expect(tick).toBeChecked();
  expect(log).toEqual(['rights']);
  await expect(tab.getByRole('button', { name: 'Add font files' })).toBeEnabled();
});

test('starts the tick on when every font is already confirmed', async ({ mount, page }) => {
  const { tab, log } = await open(page, mount, [view({}, templateEditor.assets.map(confirmed))]);
  await expect(
    tab.getByRole('checkbox', { name: 'I have the right to serve these fonts on the web' }),
  ).toBeChecked();
  await expect(tab.getByRole('button', { name: 'Add font files' })).toBeEnabled();
  expect(log).toEqual([]);
});

test('uploads a font after the rights tick', async ({ mount, page }) => {
  const added = [...notFont, storedAsset(id, 'new-tok0', 'font', 'brand.woff2', true)];
  const { tab, log } = await open(page, mount, [noFonts, noFonts, view({ fonts: [] }, added)]);
  await tab.getByRole('checkbox', { name: /right to serve/ }).check();
  await tab.locator('input[type=file]').setInputFiles(woff2('brand.woff2'));

  await expect(tab.getByText('Uploaded, not added yet')).toBeVisible();
  await expect(tab.getByText('brand.woff2, 0.56 em')).toBeVisible();
  expect(log).toEqual([
    'rights',
    'start font brand.woff2 rights=true',
    'put brand.woff2 3 no-cookie',
  ]);
  await expect(tab.getByRole('button', { name: 'Add font files' })).toBeFocused();
});

test('shows the server’s refusal', async ({ mount, page }) => {
  const { tab } = await open(
    page,
    mount,
    [noFonts],
    {},
    {
      put: () => ({
        status: 400,
        json: {
          error: 'font_not_woff2',
          message: 'Fonts must be woff2 files. This one is TrueType.',
        },
      }),
    },
  );
  await tab.getByRole('checkbox', { name: /right to serve/ }).check();
  await tab.locator('input[type=file]').setInputFiles(woff2('old.woff2'));

  const row = tab.getByRole('listitem').filter({ hasText: 'old.woff2' });
  await expect(row.getByText('refused', { exact: true })).toBeVisible();
  await expect(row.getByRole('alert')).toHaveText(
    'Fonts must be woff2 files. This one is TrueType.',
  );
  await expect(tab.getByRole('button', { name: 'Add font files' })).toBeFocused();
});

test('uploads the rest when one file is refused', async ({ mount, page }) => {
  const added = [
    ...notFont,
    storedAsset(id, 'new-tok0', 'font', 'one.woff2', true),
    storedAsset(id, 'new-tok2', 'font', 'three.woff2', true),
  ];
  const { tab, log } = await open(
    page,
    mount,
    [noFonts, noFonts, view({ fonts: [] }, added)],
    {},
    {
      put: (name) =>
        name === 'two.woff2'
          ? {
              status: 400,
              json: {
                error: 'font_not_woff2',
                message: 'Fonts must be woff2 files. This one is TrueType.',
              },
            }
          : undefined,
    },
  );
  await tab.getByRole('checkbox', { name: /right to serve/ }).check();
  await tab
    .locator('input[type=file]')
    .setInputFiles([woff2('one.woff2'), woff2('two.woff2'), woff2('three.woff2')]);

  await expect(tab.getByText('one.woff2, 0.56 em')).toBeVisible();
  await expect(tab.getByText('three.woff2, 0.56 em')).toBeVisible();
  await expect(tab.getByRole('alert')).toHaveCount(1);
  // One file after another: each start is followed by its own put.
  expect(log.slice(1).map((line) => line.split(' ').slice(0, 2).join(' '))).toEqual([
    'start font',
    'put one.woff2',
    'start font',
    'put two.woff2',
    'start font',
    'put three.woff2',
  ]);
});

test('shows a message when the network fails', async ({ mount, page }) => {
  const { tab } = await open(page, mount, [noFonts], {}, { put: () => 'network-down' });
  await tab.getByRole('checkbox', { name: /right to serve/ }).check();
  await tab.locator('input[type=file]').setInputFiles(woff2('brand.woff2'));
  await expect(tab.getByRole('alert')).toHaveText('Could not upload brand.woff2. Try again.');
});

test('removes a font from the draft and returns focus', async ({ mount, page }) => {
  const [first, ...rest] = templateEditor.manifest.fonts;
  const { tab, requests } = await open(page, mount, [templateEditor], {
    save: { json: view({ fonts: rest }) },
  });
  await expect(tab.getByText('northwind-sans-700.woff2', { exact: true })).toBeVisible();
  await expect(tab.getByText('Northwind Sans, head, weight 700, normal, 0.56 em')).toBeVisible();

  await tab.getByRole('button', { name: 'Remove northwind-sans-700.woff2' }).click();

  await expect(tab.getByRole('button', { name: 'Remove northwind-sans-700.woff2' })).toHaveCount(0);
  // The stored file stays, offered again to add.
  await expect(tab.getByText('Uploaded, not added yet')).toBeVisible();
  await expect(tab.getByRole('button', { name: 'Remove northwind-sans-800.woff2' })).toBeFocused();
  const saves = requests.filter((request) => request.call === 'save');
  expect(sentManifest(saves[0].body).fonts).toEqual(rest);
  expect(first.file).toBe('cf3f73c9bd0762db7f159baca0c9f006');
});

test('writes the asset id into the manifest', async ({ mount, page }) => {
  const agentFont = storedAsset(id, 'f00dfeed', 'font', 'agent.woff2', true);
  const withFont = view({ fonts: [] }, [...notFont, agentFont]);
  const saved = view(
    {
      fonts: [
        { role: 'body', family: 'Northwind Text', file: 'f00dfeed', weight: 500, style: 'italic' },
      ],
    },
    [...notFont, agentFont],
  );
  const { tab, requests } = await open(page, mount, [withFont], { save: { json: saved } });

  await tab.getByLabel('Role of agent.woff2').selectOption('body');
  await expect(tab.getByLabel('Family of agent.woff2')).toHaveValue('Northwind Text');
  await tab.getByLabel('Weight of agent.woff2').fill('500');
  await tab.getByLabel('Style of agent.woff2').selectOption('italic');
  await tab.getByRole('button', { name: 'Add agent.woff2 to the template' }).click();

  await expect(tab.getByText('Northwind Text, body, weight 500, italic, 0.56 em')).toBeVisible();
  const saves = requests.filter((request) => request.call === 'save');
  expect(sentManifest(saves[0].body).fonts).toEqual([
    { role: 'body', family: 'Northwind Text', file: 'f00dfeed', weight: 500, style: 'italic' },
  ]);
});

test('shows a refused family beside its field and keeps what was typed', async ({
  mount,
  page,
}) => {
  const { tab } = await open(page, mount, [templateEditor], {
    save: {
      status: 400,
      json: {
        error: 'manifest_invalid',
        message: 'type.head: must not be empty',
        path: 'type.head',
      },
    },
  });
  const head = tab.getByLabel('Heading family');
  await head.fill('');
  await head.blur();
  await expect(tab.getByText('type.head: must not be empty')).toBeVisible();
  await expect(head).toBeFocused();
  await expect(head).toHaveValue('');
});

test('screenshot: fonts tab with a refused file', async ({ mount, page }) => {
  const { tab } = await open(
    page,
    mount,
    [templateEditor],
    {},
    {
      put: () => ({
        status: 400,
        json: {
          error: 'font_not_woff2',
          message: 'Fonts must be woff2 files. This one is TrueType.',
        },
      }),
    },
  );
  await tab.getByRole('checkbox', { name: /right to serve/ }).check();
  await tab.locator('input[type=file]').setInputFiles(woff2('northwind-serif.woff2'));
  await expect(tab.getByRole('alert')).toBeVisible();
  // Tall enough that the whole tab fits: scrolled, the app's sticky top bar would sit over the top of the shot.
  await page.setViewportSize({ width: page.viewportSize()!.width, height: 1400 });
  await expect(tab).toHaveScreenshot('fonts-tab-refused.png');
});
