import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import deviceSessionCodeJson from '@contracts/screens/device-session-code.json' with { type: 'json' };
import type {
  DeviceSessionCodeRequest,
  DeviceSessionCodeView,
} from '@slideworks/core/deviceSessions';
import { AppRoutes } from '../AppRoutes';
import { me } from '../test-support/fixtures/screens';
import type { Widen } from '../test-support/fixtures/widen';
import { gate } from '../test-support/gate';
import { stubApi } from '../test-support/stubApi';
import { DEVICE_SIGN_IN_TEST_IDS as IDS } from './deviceSignIn/testIds';

const deviceSessionCode = deviceSessionCodeJson satisfies Widen<DeviceSessionCodeView>;

const CODES_PATH = '/api/auth/device-sessions/codes';
const challenge = 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM';
const state = 'Zm9vYmFyYmF6cXV4MTIz';
const device = "Mira's iPhone";

function confirmRoute(
  query: Record<string, string> = { challenge, state, device: `  ${device} ` },
) {
  return { hooksConfig: { route: `/app/sign-in?${new URLSearchParams(query).toString()}` } };
}

// Answers the mint with `answer` and keeps every request body it was sent.
async function stubMint(page: Page, answer: { status?: number; json?: unknown } | 'network-down') {
  const bodies: DeviceSessionCodeRequest[] = [];
  await page.route(`**${CODES_PATH}`, (route) => {
    bodies.push(route.request().postDataJSON() as DeviceSessionCodeRequest);
    return answer === 'network-down'
      ? route.abort('connectionrefused')
      : route.fulfill({ status: answer.status ?? 200, json: answer.json ?? {} });
  });
  return bodies;
}

test('shows device and email and the warning', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(<AppRoutes />, confirmRoute());

  await expect(page.getByTestId(IDS.heading)).toHaveText(`Sign in to SlideWorks on ${device}?`);
  await expect(page.getByTestId(IDS.email)).toHaveText(`You're signing in as ${me.email}.`);
  await expect(page.getByTestId(IDS.warning)).toHaveText(
    'Only continue if you just tapped Sign in in the SlideWorks app.',
  );
  await expect(page.getByRole('button', { name: 'Continue' })).toBeEnabled();
  await expect(page.getByRole('link', { name: 'Cancel' })).toBeVisible();
  await expect(page.getByTestId(IDS.openApp)).toHaveCount(0);
});

test("Continue mints and shows Open the app with the server's URL", async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const bodies = await stubMint(page, { json: deviceSessionCode });
  await mount(<AppRoutes />, confirmRoute());

  await page.getByTestId(IDS.continueButton).click();

  const openApp = page.getByRole('link', { name: 'Open the app' });
  await expect(openApp).toHaveAttribute('href', deviceSessionCode.openAppUrl);
  await expect(openApp).toBeFocused();
  expect(bodies).toEqual([{ challenge, state, deviceName: device }]);
  await expect(page.getByTestId(IDS.continueButton)).toHaveCount(0);
  await expect(page.getByTestId(IDS.cancel)).toHaveCount(0);
});

test('Cancel goes to the app with error=cancelled, state and iss', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const bodies = await stubMint(page, { json: deviceSessionCode });
  await mount(<AppRoutes />, confirmRoute());
  const origin = await page.evaluate(() => window.location.origin);

  await expect(page.getByRole('link', { name: 'Cancel' })).toHaveAttribute(
    'href',
    `slideworks://signed-in?error=cancelled&state=${state}&iss=${encodeURIComponent(origin)}`,
  );
  expect(bodies).toEqual([]);
});

test('incomplete link shows the restart message and no buttons', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  const bodies = await stubMint(page, { json: deviceSessionCode });
  // The state is too short to be one the app made.
  await mount(<AppRoutes />, confirmRoute({ challenge, state: 'short', device }));

  await expect(page.getByTestId(IDS.incomplete)).toHaveText(
    'This sign-in link is incomplete. Start again from the SlideWorks app.',
  );
  await expect(page.getByRole('button')).toHaveCount(0);
  await expect(page.getByRole('link')).toHaveCount(0);
  expect(bodies).toEqual([]);
});

for (const [name, query] of [
  ['no challenge', { state, device }],
  ['a short challenge', { challenge: challenge.slice(1), state, device }],
  ['a state with a character outside base64url', { challenge, state: `${state}+`, device }],
  ['a blank device', { challenge, state, device: '   ' }],
  ['a device over 80 characters', { challenge, state, device: 'x'.repeat(81) }],
] as const) {
  test(`incomplete link: ${name} shows the restart message`, async ({ mount, page }) => {
    await stubApi(page, { '/api/me': { json: me } });
    await mount(<AppRoutes />, confirmRoute(query));

    await expect(page.getByTestId(IDS.incomplete)).toBeVisible();
    await expect(page.getByTestId(IDS.continueButton)).toHaveCount(0);
  });
}

test("identity proxy shows the server's message and no link", async ({ mount, page }) => {
  const message =
    "The SlideWorks app can't sign in to this server, because it sits behind an identity proxy.";
  await stubApi(page, { '/api/me': { json: me } });
  await stubMint(page, { status: 400, json: { error: 'identity_proxy', message } });
  await mount(<AppRoutes />, confirmRoute());

  await page.getByTestId(IDS.continueButton).click();

  await expect(page.getByTestId(IDS.refused)).toHaveText(message);
  await expect(page.getByTestId(IDS.refused)).toBeFocused();
  await expect(page.getByTestId(IDS.openApp)).toHaveCount(0);
  await expect(page.getByTestId(IDS.continueButton)).toHaveCount(0);
  await expect(page.getByTestId(IDS.failed)).toHaveCount(0);
});

for (const [name, answer] of [
  ['a server error', { status: 500, json: {} }],
  ['the network', 'network-down'],
] as const) {
  test(`other failures show the retry message: ${name}`, async ({ mount, page }) => {
    await stubApi(page, { '/api/me': { json: me } });
    const bodies = await stubMint(page, answer);
    await mount(<AppRoutes />, confirmRoute());

    await page.getByTestId(IDS.continueButton).click();

    await expect(page.getByTestId(IDS.failed)).toHaveText("Couldn't start the sign-in. Try again.");
    await expect(page.getByTestId(IDS.failed)).toBeFocused();
    await expect(page.getByTestId(IDS.continueButton)).toBeEnabled();
    await expect(page.getByTestId(IDS.openApp)).toHaveCount(0);
    await expect(page.getByTestId(IDS.refused)).toHaveCount(0);

    await page.getByTestId(IDS.continueButton).click();
    await expect.poll(() => bodies.length).toBe(2);
  });
}

test('buttons are inert while minting', async ({ mount, page }) => {
  const held = gate();
  let mints = 0;
  await stubApi(page, { '/api/me': { json: me } });
  await page.route(`**${CODES_PATH}`, async (route) => {
    mints += 1;
    await held.opened;
    await route.fulfill({ json: deviceSessionCode });
  });
  await mount(<AppRoutes />, confirmRoute());

  await page.getByTestId(IDS.continueButton).click();

  await expect(page.getByTestId(IDS.continueButton)).toBeDisabled();
  await expect(page.getByTestId(IDS.cancel)).toHaveAttribute('aria-disabled', 'true');
  await expect(page.getByTestId(IDS.cancel)).not.toHaveAttribute('href');
  await page.getByTestId(IDS.continueButton).click({ force: true });
  await expect.poll(() => mints).toBe(1);

  held.release();
  await expect(page.getByTestId(IDS.openApp)).toBeFocused();
  expect(mints).toBe(1);
});

test('screenshot: the confirm page', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(<AppRoutes />, confirmRoute());
  await expect(page.getByTestId(IDS.continueButton)).toBeVisible();

  await expect(page.getByTestId(IDS.page)).toHaveScreenshot('device-sign-in-confirm.png');
});

test('screenshot: Open the app after Continue', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubMint(page, { json: deviceSessionCode });
  await mount(<AppRoutes />, confirmRoute());
  await page.getByTestId(IDS.continueButton).click();
  await expect(page.getByTestId(IDS.openApp)).toBeFocused();

  await expect(page.getByTestId(IDS.page)).toHaveScreenshot('device-sign-in-open-app.png');
});
