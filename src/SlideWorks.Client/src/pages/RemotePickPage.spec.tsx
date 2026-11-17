import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { LocationProbe } from '../test-support/LocationProbe';
import { livePresentations } from '../test-support/fixtures/presenting';
import { NavigationTypeProbe } from '../test-support/NavigationTypeProbe';
import { me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

const LIST = '/api/presentations';
const DECK = livePresentations().presentations[0].deckId;

test('remote pick page shows a card per live presentation', async ({ mount, page }) => {
  const view = livePresentations();
  view.presentations.push({
    deckId: 'Zz9otherDeck1',
    title: 'Second talk',
    slide: 2,
    total: 5,
    startedLabel: 'started 1 min ago',
    windows: [
      { role: 'presenter', label: 'Firefox on Linux' },
      { role: 'audience', label: 'Chrome on Windows' },
    ],
  });
  await stubApi(page, { '/api/me': { json: me }, [LIST]: { json: view } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/remote' } });

  await expect(page.getByTestId('remote-signed-in')).toHaveText(`Signed in as ${me.name}`);
  const cards = page.getByTestId('remote-live-card');
  await expect(cards).toHaveCount(2);
  await expect(cards.first().getByTestId('remote-live-title')).toHaveText('Ship smaller');
  await expect(cards.first().getByTestId('remote-live-slide')).toHaveText(
    'Slide 8 of 16 · started 12 min ago',
  );
  await expect(cards.first().getByTestId('remote-live-window')).toHaveText(
    'Chrome on Windows · presenter view',
  );
  await expect(cards.nth(1).getByTestId('remote-live-window')).toHaveCount(2);
  await expect(page.getByTestId('remote-empty')).toHaveCount(0);
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('remote pick page control link opens the talk in speaker mode', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, [LIST]: { json: livePresentations() } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/remote' } },
  );

  await page.getByRole('link', { name: 'Control this talk: Ship smaller' }).click();
  await expect(page.getByTestId('speaker-page')).toHaveAttribute('data-deck-id', DECK);
  await expect(page.getByTestId('location')).toHaveText(`/remote/${DECK}`);
});

test('remote pick page shows the empty state when nothing is live', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, [LIST]: { json: { presentations: [] } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/remote' } });

  await expect(page.getByTestId('remote-empty')).toBeVisible();
  await expect(page.getByTestId('remote-live-card')).toHaveCount(0);
});

test('remote pick page shows a message when the list fails to load', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: me },
    [LIST]: { status: 500, json: { error: 'boom' } },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/remote' } });

  await expect(page.getByTestId('remote-error')).toBeVisible();
  await expect(page.getByTestId('remote-empty')).toHaveCount(0);
});

test('remote pick page forwards a live deck param to speaker mode with replace', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me }, [LIST]: { json: livePresentations() } });
  await mount(
    <LocationProbe>
      <NavigationTypeProbe />
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: `/remote?deck=${DECK}` } },
  );

  await expect(page.getByTestId('speaker-page')).toHaveAttribute('data-deck-id', DECK);
  await expect(page.getByTestId('location')).toHaveText(`/remote/${DECK}`);
  await expect(page.getByTestId('navigation-type')).toHaveText('REPLACE');
});

test('remote pick page lists the talks with a note when the deck param is not live', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me }, [LIST]: { json: livePresentations() } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/remote?deck=NotLiveDeck1' } });

  await expect(page.getByTestId('remote-not-live')).toHaveText("That talk isn't live right now.");
  await expect(page.getByTestId('remote-live-card')).toHaveCount(1);
  await expect(page.getByTestId('speaker-page')).toHaveCount(0);
});

test('remote pick page reads the list again when the page becomes visible', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me } });
  let reads = 0;
  await page.route(`**${LIST}`, (route) => {
    reads++;
    return route.fulfill({ json: reads === 1 ? { presentations: [] } : livePresentations() });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/remote' } });

  await expect(page.getByTestId('remote-empty')).toBeVisible();
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
  await expect(page.getByTestId('remote-live-card')).toHaveCount(1);
  await expect(page.getByTestId('remote-empty')).toHaveCount(0);
  expect(reads).toBe(2);
});
