import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { templatePublic } from '../test-support/fixtures/templates';
import { stubApi } from '../test-support/stubApi';
import { stubTemplatesApi } from '../test-support/stubTemplatesApi';
import { TemplatePublicPageHarness } from './testing/TemplatePublicPageHarness';

const id = templatePublic.id;
const route = { hooksConfig: { route: `/t/${id}` } };
const me = { json: { email: 'kai@team.example', name: 'Kai', access: 'approved' } };

const location = (page: Page) => page.getByTestId('location');

test('offers Use this template and goes to the new deck dialog', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': me });
  const requests = await stubTemplatesApi(page);
  await mount(<TemplatePublicPageHarness />, route);

  const heading = page.getByRole('heading', { name: templatePublic.name });
  await expect(heading).toBeFocused();
  await expect(page.getByTestId('template-public-owner')).toHaveText(`By ${templatePublic.owner}`);
  await expect(page.getByTestId('template-public-purpose')).toHaveText(templatePublic.purpose);
  await expect(page.getByTestId('look-preview')).toBeVisible();

  await page.getByRole('button', { name: 'Use this template' }).click();
  await expect(location(page)).toHaveText(`/decks?template=${id}`);
  expect(requests.map((request) => request.call)).toContain('useTemplate');
  expect(requests.find((request) => request.call === 'useTemplate')?.id).toBe(id);
});

test('shows a template as text only', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': me });
  const hostile = '<img src=x onerror=alert(1)><b>bold</b>';
  await stubTemplatesApi(page, {
    publicPage: {
      json: { ...templatePublic, name: hostile, owner: hostile, purpose: hostile },
    },
  });
  await mount(<TemplatePublicPageHarness />, route);

  await expect(page.getByTestId('template-public-name')).toHaveText(hostile);
  await expect(page.getByTestId('template-public-purpose')).toHaveText(hostile);
  for (const field of ['name', 'owner', 'purpose']) {
    await expect(page.getByTestId(`template-public-${field}`).locator('b, img')).toHaveCount(0);
  }
});

test('reports a failed use', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': me });
  await stubTemplatesApi(page, {
    useTemplate: [
      { status: 400, json: { error: 'not_shared', message: 'The owner stopped sharing it.' } },
      { status: 500, json: { error: 'Something went wrong. Try again.' } },
    ],
  });
  await mount(<TemplatePublicPageHarness />, route);

  const button = page.getByTestId('template-public-use');
  await button.click();
  await expect(page.getByTestId('template-public-error')).toHaveText(
    'The owner stopped sharing it.',
  );
  await expect(button).toBeFocused();
  await expect(location(page)).toHaveText(`/t/${id}`);

  await button.click();
  await expect(page.getByTestId('template-public-error')).toHaveText(
    'Could not add this template. Try again.',
  );
  await expect(button).toBeFocused();
});

test('asks a signed-out person to sign in', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Sign in.' } } });
  const requests = await stubTemplatesApi(page);
  await mount(<TemplatePublicPageHarness />, route);

  const button = page.getByRole('button', { name: 'Sign in to use this template' });
  await button.click();
  await expect(location(page)).toHaveText(`/signin?returnTo=${encodeURIComponent(`/t/${id}`)}`);
  expect(requests.map((request) => request.call)).not.toContain('useTemplate');
});

test('says so when the link is private', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': me });
  await stubTemplatesApi(page, { publicPage: { status: 404, json: { error: 'Not found.' } } });
  await mount(<TemplatePublicPageHarness />, route);

  const message = page.getByRole('heading', {
    name: 'This template is private, or the link is wrong.',
  });
  await expect(message).toBeFocused();
  await expect(page.getByTestId('template-public-name')).toHaveCount(0);
  await expect(page.getByTestId('template-public-use')).toHaveCount(0);
  await page.getByRole('link', { name: 'Go to your templates' }).click();
  await expect(location(page)).toHaveText('/templates');
});

test('shows a message when loading fails', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': me });
  await stubTemplatesApi(page, {
    publicPage: [
      { status: 500, json: { error: 'Something went wrong. Try again.' } },
      { json: templatePublic },
    ],
  });
  await mount(<TemplatePublicPageHarness />, route);

  await expect(page.getByRole('heading', { name: 'Could not load this template.' })).toBeVisible();
  await page.getByTestId('template-public-retry').click();
  await expect(page.getByTestId('template-public-name')).toHaveText(templatePublic.name);
});

test('screenshot: template public page', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': me });
  await stubTemplatesApi(page);
  await mount(<TemplatePublicPageHarness />, route);
  await expect(page.getByTestId('look-preview-slide')).toHaveCount(2);
  await expect(page).toHaveScreenshot('template-public-page.png');
});
