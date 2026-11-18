import { test, expect } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright/test';
import { templateEditor } from '../../test-support/fixtures/templates';
import type { TemplateEditorView } from '../types';
import { EditorPreview } from './EditorPreview';

const withRadius = (md: number): TemplateEditorView => ({
  ...templateEditor,
  manifest: {
    ...templateEditor.manifest,
    shape: {
      ...templateEditor.manifest.shape,
      radius: { sm: md, md, lg: md + 4, xl: md + 6 },
    },
  },
});

const radiusOfFirstFrame = (frame: Locator) =>
  frame.first().evaluate((el) => getComputedStyle(el).getPropertyValue('--sw-radius-md').trim());

test('draws three sample slides with one sentence for screen readers', async ({ mount, page }) => {
  await mount(<EditorPreview draft={templateEditor} />);
  await expect(page.getByTestId('look-preview-slide')).toHaveCount(3);
  await expect(page.getByText('Preview of three sample slides in this template')).toBeAttached();
  await expect(page.getByTestId('slide-frame')).toHaveCount(3);
  await expect(page.getByTestId('slide-frame').nth(2)).toHaveAttribute('data-dark', 'true');
});

test("redraws with the draft's look", async ({ mount, page }) => {
  const component = await mount(<EditorPreview draft={withRadius(8)} />);
  const frame = page.getByTestId('slide-frame');
  await expect.poll(() => radiusOfFirstFrame(frame)).toBe('8px');

  await component.update(<EditorPreview draft={withRadius(16)} />);
  await expect.poll(() => radiusOfFirstFrame(frame)).toBe('16px');
});
