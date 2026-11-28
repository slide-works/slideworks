import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../AppRoutes';
import { deckPlay } from '../test-support/fixtures/screens';
import { stubDeckHub } from '../test-support/stubDeckHub';

const playPath = `/api/play/${deckPlay.id}`;
const canonical = `/d/${deckPlay.id}/${deckPlay.slug}`;

// Answers every read of the play route, whatever its query, and records each one's query.
async function stubPlay(page: Page) {
  const queries: string[] = [];
  await page.route(
    (url) => url.pathname === playPath,
    (route) => {
      queries.push(new URL(route.request().url()).search);
      return route.fulfill({ json: deckPlay });
    },
  );
  return queries;
}

test('player page with ?language= asks the play route for that language', async ({
  mount,
  page,
}) => {
  const queries = await stubPlay(page);

  await mount(<AppRoutes />, { hooksConfig: { route: `${canonical}?language=th` } });

  await expect(page.getByTestId('player')).toBeVisible();
  expect(queries.length).toBeGreaterThan(0);
  expect(new Set(queries)).toEqual(new Set(['?language=th']));
});

test('player page with ?present never sends a language', async ({ mount, page }) => {
  await stubDeckHub(page);
  const queries = await stubPlay(page);

  await mount(<AppRoutes />, { hooksConfig: { route: `${canonical}?present&language=th` } });

  await expect(page.getByTestId('player')).toBeVisible();
  expect(queries.length).toBeGreaterThan(0);
  expect(new Set(queries)).toEqual(new Set(['']));
});
