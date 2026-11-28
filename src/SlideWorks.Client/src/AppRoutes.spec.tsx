import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from './AppRoutes';
import { GoTo } from './test-support/GoTo';
import { LocationProbe } from './test-support/LocationProbe';
import { DEVICE_SIGN_IN_TEST_IDS } from './pages/deviceSignIn/testIds';
import {
  adminUsers,
  card as cardView,
  deckPrint,
  me,
  sampleDeck,
} from './test-support/fixtures/screens';
import { stubApi } from './test-support/stubApi';
import { stubTemplatesApi } from './test-support/stubTemplatesApi';
import { TEMPLATES_TEST_IDS } from './templates/testIds';

test('signed out → /signin', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/oauth/consent?request=req_123#top' } },
  );
  await expect(page.getByTestId('signin-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText(
    `/signin?returnTo=${encodeURIComponent('/oauth/consent?request=req_123#top')}`,
  );
});

test('signed in → decks shell', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
  await expect(page.getByTestId('app-shell')).toBeVisible();
  await expect(page.getByTestId('decks-page')).toBeVisible();
  await expect(page.getByTestId('user-menu-avatar')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Decks' })).toBeVisible();
});

test('api down shows cannot reach', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': 'network-down' });
  await mount(<AppRoutes />, { hooksConfig: { route: '/decks' } });
  await expect(page.getByTestId('app-unreachable')).toContainText("Can't reach SlideWorks");
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('player route renders without sign-in', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/d/7Kq2xYp9LmQa/ship-smaller' } },
  );
  await expect(page.getByTestId('player-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText('/d/7Kq2xYp9LmQa/ship-smaller');
});

test('pending person on /decks lands on /pending', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...me, access: 'pending' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/decks' } },
  );
  await expect(page.getByTestId('pending-page')).toBeAttached();
  await expect(page.getByTestId('location')).toHaveText('/pending');
});

test('approved person stays on /decks', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/decks' } },
  );
  await expect(page.getByTestId('decks-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText('/decks');
});

test('/admin/users renders inside the shell', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: { ...me, isAdmin: true } },
    '/api/admin/users': { json: adminUsers },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/admin/users' } });
  await expect(page.getByTestId('app-shell')).toBeVisible();
  await expect(page.getByTestId('admin-users-page')).toBeVisible();
  await expect(page.getByTestId('admin-user-row')).toHaveCount(adminUsers.users.length);
});

test('/pending renders without the shell', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...me, access: 'pending' } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/pending' } });
  await expect(page.getByTestId('pending-page')).toBeVisible();
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('render route renders without sign-in', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { status: 401, json: { error: 'Not signed in' } },
    '/api/render/7Kq2xYp9LmQa/session': { json: {} },
    '/api/render/7Kq2xYp9LmQa': { json: { ...sampleDeck, id: '7Kq2xYp9LmQa' } },
  });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/render/7Kq2xYp9LmQa/3#token=abc.def' } },
  );
  const slide = page.getByTestId('render-slide');
  await expect(slide).toBeVisible();
  await expect(slide).toHaveAttribute('data-deck-id', '7Kq2xYp9LmQa');
  await expect(slide).toHaveAttribute('data-slide', '3');
  await expect(slide).not.toHaveAttribute('data-error');
  await expect(page.getByTestId('location')).toHaveText('/render/7Kq2xYp9LmQa/3#token=abc.def');
});

test('print route renders without sign-in', async ({ mount, page }) => {
  await page.route(`**/api/images/img_logo0001`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
  await stubApi(page, {
    '/api/me': { status: 401, json: { error: 'Not signed in' } },
    [`/api/render/${deckPrint.deck.id}/session`]: { json: {} },
    [`/api/render/${deckPrint.deck.id}/print`]: { json: deckPrint },
  });
  await mount(<AppRoutes />, {
    hooksConfig: { route: `/print/${deckPrint.deck.id}#token=abc.def` },
  });
  const root = page.getByTestId('print-deck');
  await expect(root).toBeVisible();
  await expect(root).toHaveAttribute('data-deck-id', deckPrint.deck.id);
  await expect(root).not.toHaveAttribute('data-error', /.*/);
  await expect(page.getByTestId('print-slide')).toHaveCount(deckPrint.deck.slides.length);
});

test('render route without a token in the fragment reports it', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/render/7Kq2xYp9LmQa/3?token=abc.def' },
  });
  await expect(page.getByTestId('render-slide')).toHaveAttribute(
    'data-error',
    'Render token missing',
  );
});

test('generic card route renders without sign-in or token', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/card/generic' } },
  );
  const card = page.getByTestId('share-card');
  await expect(card).toBeVisible();
  await expect(card).toHaveAttribute('data-generic', 'true');
  await expect(card).not.toHaveAttribute('data-error');
  await expect(page.getByTestId('location')).toHaveText('/card/generic');
});

test("a deck's card route renders without sign-in", async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { status: 401, json: { error: 'Not signed in' } },
    '/api/render/7Kq2xYp9LmQa/card': {
      json: { ...cardView, look: { ...cardView.look, logo: null } },
    },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/card/7Kq2xYp9LmQa#token=abc.def' } });
  const card = page.getByTestId('share-card');
  await expect(card).toHaveAttribute('data-deck-id', '7Kq2xYp9LmQa');
  await expect(card).toHaveAttribute('data-generic', 'false');
  await expect(card).not.toHaveAttribute('data-error');
});

test('/remote signed out → /signin keeping the deck in returnTo', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/remote?deck=x' } },
  );
  await expect(page.getByTestId('signin-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText(
    `/signin?returnTo=${encodeURIComponent('/remote?deck=x')}`,
  );
});

test('/decks/:id/present signed out → /signin with returnTo', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/decks/7Kq2abcdEFGH/present' } },
  );
  await expect(page.getByTestId('signin-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText(
    `/signin?returnTo=${encodeURIComponent('/decks/7Kq2abcdEFGH/present')}`,
  );
});

test('/decks/:id/present signed in renders the presenter window without the shell', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/decks/7Kq2abcdEFGH/present' } });
  await expect(page.getByTestId('presenter-page')).toHaveAttribute('data-deck-id', '7Kq2abcdEFGH');
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('/remote signed in renders the pick page without the shell', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/remote' } });
  await expect(page.getByTestId('remote-pick')).toBeVisible();
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('/remote/:id signed in renders speaker mode without the shell', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/remote/7Kq2abcdEFGH' } });
  await expect(page.getByTestId('speaker-page')).toHaveAttribute('data-deck-id', '7Kq2abcdEFGH');
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('pending person on /remote lands on /pending', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...me, access: 'pending' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/remote' } },
  );
  await expect(page.getByTestId('pending-page')).toBeAttached();
  await expect(page.getByTestId('location')).toHaveText('/pending');
});

test('templates routes resolve', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(
    <GoTo>
      <LocationProbe>
        <AppRoutes />
      </LocationProbe>
    </GoTo>,
    { hooksConfig: { route: '/decks' } },
  );
  await expect(page.getByTestId('app-shell')).toBeVisible();

  await page.getByRole('link', { name: 'Templates' }).click();
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.page)).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText('/templates');

  for (const [path, testId] of [
    ['/templates/tpl_Nw4kT8pQz2Lm/edit', TEMPLATES_TEST_IDS.editor],
    ['/templates/tpl_Nw4kT8pQz2Lm/layouts/stage-ladder', TEMPLATES_TEST_IDS.layoutReviewPage],
  ]) {
    await page.getByTestId('goto-path').fill(path);
    await page.getByTestId('goto').click();
    await expect(page.getByTestId(testId)).toBeVisible();
    await expect(page.getByTestId('app-shell')).toBeVisible();
    await expect(page.getByTestId('location')).toHaveText(path);
  }
});

test("a template's page renders without sign-in or the shell", async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await stubTemplatesApi(page);
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/t/tpl_Nw4kT8pQz2Lm' } },
  );
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.publicPage)).toBeVisible();
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
  await expect(page.getByTestId('location')).toHaveText('/t/tpl_Nw4kT8pQz2Lm');
});

const appSignIn =
  '/app/sign-in?challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&state=Zm9vYmFyYmF6cXV4MTIz&device=Pixel';

test('app sign-in sends signed-out people to sign in and back', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { status: 401, json: { error: 'Not signed in' } },
    '/api/signin/options': { json: { google: true } },
  });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: appSignIn } },
  );
  await expect(page.getByTestId('signin-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText(
    `/signin?returnTo=${encodeURIComponent(appSignIn)}`,
  );
  await expect(page.getByTestId('signin-google')).toHaveAttribute(
    'href',
    `/signin/google?returnTo=${encodeURIComponent(appSignIn)}`,
  );
});

test('app sign-in lets pending people through', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...me, access: 'pending' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: appSignIn } },
  );
  await expect(page.getByTestId(DEVICE_SIGN_IN_TEST_IDS.continueButton)).toBeVisible();
  await expect(page.getByTestId('pending-page')).toHaveCount(0);
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
  await expect(page.getByTestId('location')).toHaveText(appSignIn);
});
