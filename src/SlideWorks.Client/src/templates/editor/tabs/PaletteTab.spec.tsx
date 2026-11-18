import { test, expect } from '@playwright/experimental-ct-react';
import { editorTabProps, templateEditor } from '../../../test-support/fixtures/templates';
import type { TemplateEditorView, TemplateManifest, TemplateManifestPalette } from '../../types';
import type { ChangeOutcome } from '../../useTemplateDraft';
import { PaletteTab } from './PaletteTab';

const palette = templateEditor.manifest.palette!;

const withPalette = (
  changes: Partial<TemplateManifestPalette>,
  lock?: boolean,
): TemplateEditorView => ({
  ...templateEditor,
  manifest: {
    ...templateEditor.manifest,
    palette: { ...palette, ...changes },
    ...(lock === undefined ? {} : { lockPalette: lock }),
  },
});

const FAILING = withPalette({ dd: '#ea580c' });

// A change that records the patches it was given and says each was saved.
const recorder = (answers = true) => {
  const patches: Partial<TemplateManifest>[] = [];
  const change = (patch: Partial<TemplateManifest>): Promise<ChangeOutcome> => {
    patches.push(patch);
    // A save that has not answered yet keeps what was typed on screen, as the real one does.
    return answers ? Promise.resolve({ ok: true }) : new Promise<ChangeOutcome>(() => undefined);
  };
  return { patches, change };
};

test('re-runs contrast when a colour changes', async ({ mount, page }) => {
  const { patches, change } = recorder(false);
  const component = await mount(<PaletteTab {...editorTabProps({ change })} />);
  await expect(page.getByText('Every contrast pair passes.')).toBeVisible();
  await expect(page.getByTestId('palette-failing-pair')).toHaveCount(0);

  await page.getByLabel('dd', { exact: true }).fill('#ea580c');
  await expect(page.getByText('1 contrast pair fails.')).toBeVisible();
  const pair = page.getByTestId('palette-failing-pair');
  await expect(pair).toContainText('Deep-dive pill needs 4.5:1');
  await expect(pair).toContainText(/\d\.\d:1/);
  expect(patches).toHaveLength(1);
  expect(patches[0]?.palette?.dd).toBe('#ea580c');

  await component.update(<PaletteTab {...editorTabProps({ change, draft: FAILING })} />);
  await expect(page.getByTestId('palette-failing-pair')).toHaveCount(1);
  await expect(page.getByLabel('dd', { exact: true })).toHaveValue('#ea580c');
});

test('offers the nearest passing colour and applies it only on request', async ({
  mount,
  page,
}) => {
  const { patches, change } = recorder();
  const component = await mount(<PaletteTab {...editorTabProps({ change, draft: FAILING })} />);
  const use = page.getByRole('button', { name: /^Use #[0-9A-F]{6} for the deep-dive pill$/ });
  await expect(use).toBeVisible();
  // Nothing changed a colour by itself.
  expect(patches).toHaveLength(0);
  await expect(page.getByLabel('dd', { exact: true })).toHaveValue('#ea580c');

  await use.click();
  expect(patches).toHaveLength(1);
  const saved = patches[0]?.palette;
  expect(saved).toBeDefined();
  expect(saved!.dd).not.toBe('#ea580c');
  await component.update(
    <PaletteTab
      {...editorTabProps({
        change,
        draft: withPalette({ dd: saved!.dd, 'on-dd': saved!['on-dd'] }),
      })}
    />,
  );
  await expect(page.getByTestId('palette-failing-pair')).toHaveCount(0);
});

test('adds an extra colour with its ink', async ({ mount, page }) => {
  const { patches, change } = recorder(false);
  const draft = withPalette({ extras: [{ name: 'extra-1', colour: '#050814', ink: '#ffffff' }] });
  const component = await mount(<PaletteTab {...editorTabProps({ change, draft })} />);
  await expect(page.getByTestId('palette-extra')).toHaveCount(1);
  await page.getByRole('button', { name: 'Add an extra colour' }).click();
  expect(patches[0]?.palette?.extras?.map((extra) => extra.name)).toEqual(['extra-1', 'extra-2']);

  const four = withPalette({
    extras: ['extra-1', 'extra-2', 'extra-3', 'extra-4'].map((name) => ({
      name: name as 'extra-1',
      colour: '#050814',
      ink: '#ffffff',
    })),
  });
  await component.update(<PaletteTab {...editorTabProps({ change, draft: four })} />);
  await expect(page.getByRole('button', { name: 'Add an extra colour' })).toBeDisabled();

  // An ink that does not read on its colour is flagged.
  await page.getByLabel('extra-4 ink').fill('#060a18');
  await expect(
    page.getByText(/extra-4 ink is \d\.\d:1 on its colour and needs 4\.5:1/),
  ).toBeVisible();
});

test('refuses a colour that is not hex', async ({ mount, page }) => {
  const { patches, change } = recorder();
  await mount(<PaletteTab {...editorTabProps({ change })} />);
  const input = page.getByLabel('accent', { exact: true });
  await input.fill('blue');
  await expect(page.getByText('Use a colour like #0F766E.')).toBeVisible();
  await expect(input).toHaveAttribute('aria-invalid', 'true');
  expect(patches).toHaveLength(0);
  // The preview-facing contrast check still reads the last good colour.
  await expect(page.getByText('Every contrast pair passes.')).toBeVisible();
});

test('shows the sentence of a refused colour and focuses the field', async ({ mount, page }) => {
  const change = (): Promise<ChangeOutcome> =>
    Promise.resolve({
      ok: false,
      code: 'manifest_invalid',
      message: 'palette.accent must be a colour.',
      path: 'palette.accent',
    });
  await mount(<PaletteTab {...editorTabProps({ change })} />);
  await page.getByLabel('accent', { exact: true }).fill('#123456');
  await expect(page.getByText('palette.accent must be a colour.')).toBeVisible();
  await expect(page.getByLabel('accent', { exact: true })).toBeFocused();
});

test('locks the palette', async ({ mount, page }) => {
  const { patches, change } = recorder();
  await mount(<PaletteTab {...editorTabProps({ change, draft: withPalette({}, false) })} />);
  const lock = page.getByRole('checkbox', { name: 'Lock this palette' });
  await expect(lock).not.toBeChecked();
  await lock.click();
  expect(patches).toEqual([{ lockPalette: true }]);
});

test('screenshot: palette tab with a failing pair', async ({ mount, page }) => {
  await mount(<PaletteTab {...editorTabProps({ draft: FAILING })} />);
  await expect(page.getByTestId('palette-failing-pair')).toBeVisible();
  await expect(page).toHaveScreenshot('palette-tab-failing-pair.png');
});
