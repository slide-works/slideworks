import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { guestView } from '../test-support/fixtures/guestStates';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { GUEST_TEST_IDS } from './testIds';

const joinPath = '/api/join/482913';

async function mountWith(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  page: Parameters<Parameters<typeof test>[2]>[0]['page'],
  slide: { layout: string; slots: object; surface: 'light' | 'dark' },
) {
  const view = guestView('live');
  const state = {
    ...view.state,
    current: { ...view.state.current!, ...slide, steps: 0, step: 0 },
  } as typeof view.state;
  await stubDeckHub(page, { audience: { state } });
  await stubApi(page, { [joinPath]: { json: { ...view, state } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.slide)).toBeVisible();
}

test('a dark slide on the phone draws on night', async ({ mount, page }) => {
  await mountWith(mount, page, {
    layout: 'statement',
    slots: { statement: 'Ship smaller' },
    surface: 'dark',
  });
  await expect(page.getByTestId(GUEST_TEST_IDS.slide).locator('.sw-frame')).toHaveAttribute(
    'data-surface',
    'dark',
  );
});

test('a slide with a light surface draws as before', async ({ mount, page }) => {
  await mountWith(mount, page, {
    layout: 'statement',
    slots: { statement: 'Ship smaller' },
    surface: 'light',
  });
  await expect(page.getByTestId(GUEST_TEST_IDS.slide).locator('.sw-frame')).toHaveAttribute(
    'data-surface',
    'light',
  );
});
