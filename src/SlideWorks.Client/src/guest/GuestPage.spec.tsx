import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { me } from '../test-support/fixtures/screens';
import { LocationProbe } from '../test-support/LocationProbe';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { GuestScreens } from './GuestScreens';
import { GUEST_TEST_IDS, JOIN_CODE_TEST_IDS } from './testIds';

const joinPath = '/api/join/482913';

test('live → "you\'re in", then the slide mirror with the waiting card', async ({
  mount,
  page,
}) => {
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await stubApi(page, { [joinPath]: { json: guestView('live') } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.page)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.joined)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.following)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.activityWaiting)).toBeVisible();
});

test('lobby → the lobby', async ({ mount, page }) => {
  await stubApi(page, { [joinPath]: { json: guestView('lobby') } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.lobby)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.following)).toHaveCount(0);
});

test('ended → the thank-you, with the slides link when the deck is shared', async ({
  mount,
  page,
}) => {
  await stubApi(page, { [joinPath]: { json: guestView('ended') } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.ended)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.endedSlides)).toHaveAttribute(
    'href',
    'https://slideworks.example/d/7Kq2xYp9LmQa/ship-smaller',
  );
});

test('an unknown code keeps the page and asks for the code again', async ({ mount, page }) => {
  await stubApi(page, { [joinPath]: { status: 404, json: { error: 'no_session' } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.page)).toBeVisible();
  await expect(page.getByTestId(JOIN_CODE_TEST_IDS.error)).toContainText('482913');
});

test('a failed read says the phone cannot reach the talk', async ({ mount, page }) => {
  await stubApi(page, { [joinPath]: { status: 500, json: { error: 'boom' } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.offline)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.offlineRetry)).toBeVisible();
});

test('/482913 mounts the guest page without a sign-in', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { status: 401, json: { error: 'Not signed in' } },
    [joinPath]: { json: guestView('lobby') },
  });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/482913' } },
  );
  await expect(page.getByTestId(GUEST_TEST_IDS.page)).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText('/482913');
});

test('/123 is not a code and does not reach the guest page', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/123' } },
  );
  await expect(page.getByTestId('location')).toHaveText('/decks');
  await expect(page.getByTestId(GUEST_TEST_IDS.page)).toHaveCount(0);
});

test('/join mounts the keypad page', async ({ mount, page }) => {
  await stubApi(page, {});
  await mount(<AppRoutes />, { hooksConfig: { route: '/join' } });
  await expect(page.getByTestId(JOIN_CODE_TEST_IDS.page)).toBeVisible();
});

test('offline shows the offline screen instead of the last one', async ({ mount, page }) => {
  await mount(
    <GuestScreens
      view={guestView('lobby')}
      state={audienceState({ phase: 'lobby', current: null })}
      status="offline"
      onRetry={() => undefined}
    />,
  );
  await expect(page.getByTestId(GUEST_TEST_IDS.offline)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.lobby)).toHaveCount(0);
});

test('reconnecting keeps the screen and adds the banner', async ({ mount, page }) => {
  await mount(
    <GuestScreens
      view={guestView('lobby')}
      state={audienceState({ phase: 'lobby', current: null })}
      status="reconnecting"
      onRetry={() => undefined}
    />,
  );
  await expect(page.getByTestId(GUEST_TEST_IDS.lobby)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.reconnecting)).toBeVisible();
});

test('the privacy sheet opens from the footer and closes', async ({ mount, page }) => {
  await mount(
    <GuestScreens
      view={guestView('lobby')}
      state={audienceState({ phase: 'lobby', current: null })}
      status="live"
      onRetry={() => undefined}
    />,
  );
  await page.getByTestId(GUEST_TEST_IDS.privacyOpen).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.privacySheet)).toBeVisible();
  await page.getByTestId(GUEST_TEST_IDS.privacyClose).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.privacySheet)).toHaveCount(0);
});
