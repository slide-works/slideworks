import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { me } from '../../test-support/fixtures/screens';
import { templateEditor } from '../../test-support/fixtures/templates';
import { stubApi } from '../../test-support/stubApi';
import { stubTemplatesApi } from '../../test-support/stubTemplatesApi';
import { editorTabButtonTestId, editorTabTestId } from '../testIds';

test('the Custom layouts tab is listed', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: `/templates/${templateEditor.id}/edit` } });

  const button = page.getByTestId(editorTabButtonTestId('layouts'));
  await expect(button).toHaveText('Custom layouts');
  await button.click();

  await expect(button).toHaveAttribute('aria-selected', 'true');
  const tab = page.getByTestId(editorTabTestId('layouts'));
  await expect(tab.getByRole('heading', { name: 'Custom layouts', level: 2 })).toBeVisible();
  await expect(tab.getByRole('button', { name: 'Upload a custom layout' })).toBeVisible();
});
