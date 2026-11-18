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

  await expect(page.getByRole('heading', { name: 'Edit Northwind kickoff' })).toBeVisible();
  await expectOpen(page, 'logo-art');
  await expect(
    page.getByTestId(TEMPLATES_TEST_IDS.editorPanel).getByRole('heading', { name: 'Logo and art' }),
  ).toBeVisible();
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

test('shows a message when the template cannot be read', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page, { get: { status: 404, json: { error: 'No such template.' } } });
  await mount(<AppRoutes />, at());

  const error = page.getByTestId(TEMPLATES_TEST_IDS.editorError);
  await expect(error).toHaveText('No such template.');
  await expect(error).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.editorTabs)).toHaveCount(0);
});

test('a tab mounts alone with props built from the fixture', async ({ mount, page }) => {
  await mount(<FontsTab {...editorTabProps()} />);

  await expect(page.getByTestId(editorTabTestId('fonts'))).toBeVisible();
  await expect(page.getByRole('region', { name: 'Fonts of Northwind kickoff' })).toBeVisible();
});
