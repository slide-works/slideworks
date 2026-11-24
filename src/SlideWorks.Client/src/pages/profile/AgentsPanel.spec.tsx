import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { me, profile } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';

const route = { hooksConfig: { route: '/profile' } };
const claude = profile.agents[0]!;
const agentUrl = `**/api/me/agents/${claude.id}`;

test('disconnect shows the question and sends nothing', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  const calls: string[] = [];
  await page.route(agentUrl, (r) => {
    calls.push(r.request().method());
    return r.fulfill({ status: 204 });
  });
  await mount(<AppRoutes />, route);

  await page.getByRole('button', { name: 'Disconnect Claude' }).click();

  await expect(page.getByTestId('profile-agent-confirm')).toContainText('Disconnect Claude?');
  await expect(page.getByRole('button', { name: 'Keep Claude' })).toBeVisible();
  expect(calls).toEqual([]);
});

test('keep closes the question, the row stays and no call is made', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  const calls: string[] = [];
  await page.route(agentUrl, (r) => {
    calls.push(r.request().method());
    return r.fulfill({ status: 204 });
  });
  await mount(<AppRoutes />, route);
  await page.getByRole('button', { name: 'Disconnect Claude' }).click();

  await page.getByRole('button', { name: 'Keep Claude' }).click();

  await expect(page.getByTestId('profile-agent-confirm')).toHaveCount(0);
  await expect(page.getByTestId('profile-agent')).toHaveCount(profile.agents.length);
  await expect(page.getByRole('button', { name: 'Disconnect Claude' })).toBeFocused();
  expect(calls).toEqual([]);
});

test('confirming removes the row, says so and focuses the panel heading', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  const calls: string[] = [];
  await page.route(agentUrl, (r) => {
    calls.push(r.request().method());
    return r.fulfill({ status: 204 });
  });
  await mount(<AppRoutes />, route);
  await page.getByRole('button', { name: 'Disconnect Claude' }).click();

  await page.getByTestId('profile-agent-confirm-disconnect').click();

  await expect(page.getByTestId('profile-agents-notice')).toHaveText('Claude disconnected.');
  await expect(page.getByTestId('profile-agent')).toHaveCount(profile.agents.length - 1);
  await expect(page.getByText('Cursor', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Connected agents' })).toBeFocused();
  expect(calls).toEqual(['DELETE']);
});

test('a failed disconnect shows a message and keeps the row', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  await page.route(agentUrl, (r) => r.fulfill({ status: 500, json: { error: 'Something broke' } }));
  await mount(<AppRoutes />, route);
  await page.getByRole('button', { name: 'Disconnect Claude' }).click();

  await page.getByTestId('profile-agent-confirm-disconnect').click();

  await expect(page.getByTestId('profile-agents-error')).toContainText('Claude');
  await expect(page.getByTestId('profile-agents-error')).toContainText('Something broke');
  await expect(page.getByTestId('profile-agent')).toHaveCount(profile.agents.length);
  await expect(page.getByRole('button', { name: 'Disconnect Claude' })).toBeFocused();
});

test('the panel says no agents are connected when there are none', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: me },
    '/api/me/profile': { json: { ...profile, agents: [] } },
  });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('profile-agents-empty')).toHaveText('No agents are connected.');
  await expect(page.getByTestId('profile-agent')).toHaveCount(0);
});

test('with agents the empty message is not shown', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('profile-agent')).toHaveCount(profile.agents.length);
  await expect(page.getByTestId('profile-agents-empty')).toHaveCount(0);
});
