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
const route = { hooksConfig: { route: `/templates/${id}/edit?tab=logo-art` } };
const file = (name: string, mimeType: string) => ({
  name,
  mimeType,
  buffer: Buffer.from([1, 2, 3]),
});

const view = (
  manifest: Partial<TemplateManifest>,
  assets: TemplateAsset[] = templateEditor.assets,
): TemplateEditorView => ({
  ...templateEditor,
  manifest: { ...templateEditor.manifest, ...manifest },
  assets,
});

const { light, dark, ...logosOnly } = templateEditor.manifest.logos;
const noLogoAssets = templateEditor.assets.filter(
  (asset) => asset.assetId !== light && asset.assetId !== dark,
);
const noLogos = view({ logos: logosOnly }, noLogoAssets);
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
  const tab = page.getByTestId(editorTabTestId('logo-art'));
  await expect(tab.getByRole('heading', { name: 'Logo and art', level: 2 })).toBeVisible();
  return { tab, requests, log };
}

const saves = (requests: { call: string; body: unknown }[]) =>
  requests
    .filter((request) => request.call === 'save')
    .map((request) => sentManifest(request.body));

test('sets the light and dark logo', async ({ mount, page }) => {
  const lightAsset = storedAsset(id, 'new-tok0', 'logo', 'light.svg');
  const darkAsset = storedAsset(id, 'new-tok1', 'logo', 'dark.png');
  const withLight = view({ logos: { ...logosOnly, light: 'new-tok0' } }, [
    ...noLogoAssets,
    lightAsset,
  ]);
  const withBoth = view({ logos: { ...logosOnly, light: 'new-tok0', dark: 'new-tok1' } }, [
    ...noLogoAssets,
    lightAsset,
    darkAsset,
  ]);
  const { tab, requests, log } = await open(page, mount, [noLogos, withLight, withBoth], {
    save: [{ json: withLight }, { json: withBoth }],
  });

  await tab.locator('input[type=file]').nth(0).setInputFiles(file('light.svg', 'image/svg+xml'));
  await expect(tab.getByRole('img', { name: 'Logo on light slides: light.svg' })).toHaveAttribute(
    'src',
    `/api/templates/${id}/assets/new-tok0`,
  );
  await expect(tab.getByRole('button', { name: 'Replace logo on light slides' })).toBeFocused();

  await tab.locator('input[type=file]').nth(1).setInputFiles(file('dark.png', 'image/png'));
  await expect(tab.getByRole('img', { name: 'Logo on dark slides: dark.png' })).toBeVisible();

  expect(log.map((line) => line.split(' ').slice(0, 3).join(' '))).toEqual([
    'start logo light.svg',
    'put light.svg 3',
    'start logo dark.png',
    'put dark.png 3',
  ]);
  expect(log[0]).toBe('start logo light.svg rights=false');
  expect(saves(requests).map((manifest) => manifest.logos)).toEqual([
    { ...logosOnly, light: 'new-tok0' },
    { ...logosOnly, light: 'new-tok0', dark: 'new-tok1' },
  ]);
});

test('shows the server’s refusal for an unsafe SVG', async ({ mount, page }) => {
  const message = 'This SVG contains a script. Export it again without scripts, or upload a PNG.';
  const { tab, requests } = await open(
    page,
    mount,
    [noLogos],
    {},
    {
      put: () => ({ status: 400, json: { error: 'svg_script', message } }),
    },
  );
  await tab.locator('input[type=file]').nth(0).setInputFiles(file('bad.svg', 'image/svg+xml'));

  await expect(tab.getByRole('alert')).toHaveText(message);
  await expect(tab.getByText('No logo')).toHaveCount(2);
  expect(saves(requests)).toEqual([]);
  await expect(tab.getByRole('button', { name: 'Upload logo on light slides' })).toBeFocused();
});

test('shows a message when the network fails', async ({ mount, page }) => {
  const { tab } = await open(page, mount, [noLogos], {}, { put: () => 'network-down' });
  await tab.locator('input[type=file]').nth(1).setInputFiles(file('dark.png', 'image/png'));
  await expect(tab.getByRole('alert')).toHaveText('Could not upload dark.png. Try again.');
});

test('removes the dark logo', async ({ mount, page }) => {
  const { tab, requests } = await open(page, mount, [templateEditor], {
    save: { json: view({ logos: { light, showOnDark: true } }) },
  });
  await expect(tab.getByRole('img', { name: /Logo on dark slides/ })).toBeVisible();

  await tab.getByRole('button', { name: 'Remove the dark logo' }).click();

  await expect(tab.getByRole('img', { name: /Logo on dark slides/ })).toHaveCount(0);
  await expect(tab.getByRole('button', { name: 'Upload logo on dark slides' })).toBeFocused();
  await expect(tab.getByRole('img', { name: /Logo on light slides/ })).toBeVisible();
  const [sent] = saves(requests);
  expect(sent.logos).toEqual({ light, showOnDark: true });
  expect('dark' in sent.logos).toBe(false);
});

test('switches the title art to waves', async ({ mount, page }) => {
  const waves = { kind: 'generator' as const, generator: 'waves' };
  const { tab, requests } = await open(page, mount, [templateEditor], {
    save: { json: view({ titleArt: waves }) },
  });
  await expect(tab.getByRole('radio', { name: 'An image' })).toBeChecked();

  await tab.getByRole('radio', { name: 'Waves' }).check();

  await expect(tab.getByRole('radio', { name: 'Waves' })).toBeChecked();
  await expect(tab.getByRole('button', { name: /title art image/ })).toHaveCount(0);
  expect(saves(requests).map((manifest) => manifest.titleArt)).toEqual([waves]);
});

test('sets an uploaded image as the title art', async ({ mount, page }) => {
  const art = storedAsset(id, 'new-tok0', 'art', 'art.webp');
  const noArt = view(
    { titleArt: { kind: 'none' } },
    templateEditor.assets.filter((a) => a.kind !== 'art'),
  );
  const withArt = view({ titleArt: { kind: 'image', file: 'new-tok0' } }, [...noArt.assets, art]);
  const { tab, requests } = await open(page, mount, [noArt, withArt], { save: { json: withArt } });

  await tab.getByRole('radio', { name: 'An image' }).check();
  expect(saves(requests)).toEqual([]);
  await tab.locator('input[type=file]').nth(2).setInputFiles(file('art.webp', 'image/webp'));

  await expect(tab.getByRole('img', { name: 'Title art: art.webp' })).toBeVisible();
  await expect(tab.getByRole('button', { name: 'Replace title art image' })).toBeFocused();
  expect(saves(requests).map((manifest) => manifest.titleArt)).toEqual([
    { kind: 'image', file: 'new-tok0' },
  ]);
});

test('turns off the dark logo on title slides', async ({ mount, page }) => {
  const { tab, requests } = await open(page, mount, [templateEditor], {
    save: { json: view({ logos: { ...templateEditor.manifest.logos, showOnDark: false } }) },
  });
  const tick = tab.getByRole('checkbox', {
    name: 'Show the dark logo on title, divider and close slides',
  });
  await expect(tick).toBeChecked();

  await tick.uncheck();

  await expect(tick).not.toBeChecked();
  expect(saves(requests).map((manifest) => manifest.logos)).toEqual([
    { ...templateEditor.manifest.logos, showOnDark: false },
  ]);
});

test('offers only the ten drawable dot colours', async ({ mount, page }) => {
  const { tab, requests } = await open(page, mount, [templateEditor], {
    save: {
      json: view({ ornament: { dots: [...templateEditor.manifest.ornament.dots, 'good'] } }),
    },
  });
  const group = tab.getByRole('group', { name: 'Ornament dots' });
  await expect(group.getByRole('button')).toHaveText([
    'accent',
    'accent-2',
    'accent-glow',
    'good',
    'warn',
    'bad',
    'extra-1',
    'extra-2',
    'extra-3',
    'extra-4',
  ]);
  await expect(group.getByRole('button', { name: 'extra-2' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await group.getByRole('button', { name: 'good' }).click();

  await expect(group.getByRole('button', { name: 'good' })).toHaveAttribute('aria-pressed', 'true');
  expect(saves(requests).map((manifest) => manifest.ornament.dots)).toEqual([
    ['accent', 'accent-2', 'accent-glow', 'extra-2', 'good'],
  ]);
});

test('shows a refused dot list beside the dots', async ({ mount, page }) => {
  const { tab } = await open(page, mount, [templateEditor], {
    save: {
      status: 400,
      json: {
        error: 'manifest_invalid',
        message: 'ornament.dots: can hold at most 6 dots',
        path: 'ornament.dots',
      },
    },
  });
  const group = tab.getByRole('group', { name: 'Ornament dots' });
  await group.getByRole('button', { name: 'bad' }).click();
  await expect(group.getByText('ornament.dots: can hold at most 6 dots')).toBeVisible();
  await expect(group.getByRole('button', { name: 'bad' })).toHaveAttribute('aria-pressed', 'false');
  await expect(group.getByRole('button', { name: 'bad' })).toBeFocused();
});

test('screenshot: logo and art tab', async ({ mount, page }) => {
  const { tab } = await open(page, mount, [templateEditor]);
  await expect(tab.getByRole('img', { name: /Logo on dark slides/ })).toBeVisible();
  await expect(tab).toHaveScreenshot('logo-art-tab.png');
});
