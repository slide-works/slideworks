import { test, expect } from '@playwright/experimental-ct-react';
import type { DecksLibraryView, SharedDeckRow } from '../../api/types';
import { LocationProbe } from '../../test-support/LocationProbe';
import { decksLibrary } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { DecksPage } from '../DecksPage';

const route = { hooksConfig: { route: '/decks' } };

const readRow: SharedDeckRow = { ...decksLibrary.sharedWithMe[0], access: 'read' };
const writeRow: SharedDeckRow = {
  ...readRow,
  id: 'Wr1teDeck0001',
  slug: 'roadmap-draft',
  title: 'Roadmap draft',
  playUrl: '/d/Wr1teDeck0001/roadmap-draft',
  access: 'write',
};

async function mountLibrary(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  page: Parameters<Parameters<typeof test>[2]>[0]['page'],
  rows: SharedDeckRow[],
) {
  const view: DecksLibraryView = { ...decksLibrary, sharedWithMe: rows };
  await stubApi(page, { '/api/decks': { json: view } });
  await mount(
    <LocationProbe>
      <DecksPage />
    </LocationProbe>,
    route,
  );
}

test('SharedWithMeRow_WhenWrite_ShouldSayCanEditAndOpenDeckPage', async ({ mount, page }) => {
  await mountLibrary(mount, page, [writeRow, readRow]);

  const rows = page.getByTestId('shared-with-me').getByTestId('shared-row');
  await expect(rows).toHaveCount(2);
  await expect(page.getByRole('columnheader', { name: 'Access' })).toBeVisible();

  const write = rows.filter({ hasText: 'Roadmap draft' });
  await expect(write.getByTestId('shared-access')).toHaveText('Can edit');
  await expect(write.getByTestId('shared-access')).not.toHaveText('Can view');

  await write.getByTestId('deck-title').click();
  await expect(page.getByTestId('location')).toHaveText('/decks/Wr1teDeck0001');
});

test('SharedWithMeRow_WhenRead_ShouldSayCanViewAndOpenPlayer', async ({ mount, page }) => {
  await mountLibrary(mount, page, [writeRow, readRow]);

  const read = page
    .getByTestId('shared-with-me')
    .getByTestId('shared-row')
    .filter({ hasText: readRow.title });
  await expect(read.getByTestId('shared-access')).toHaveText('Can view');
  await expect(read.getByTestId('shared-access')).not.toHaveText('Can edit');

  await read.getByTestId('deck-title').click();
  await expect(page.getByTestId('location')).toHaveText('/d/Ob3nWk1Jn9Ef/onboarding-week-one');
});
