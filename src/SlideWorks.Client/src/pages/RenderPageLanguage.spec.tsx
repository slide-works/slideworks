import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../AppRoutes';
import { sampleDeck } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

const TOKEN = 'preview-token';
const renderPath = `/api/render/${sampleDeck.id}`;
const slide = sampleDeck.slides[0]!;

// Answers the session and the read, whatever their query, and records each request as "METHOD path?query". The sample
// image is served too.
async function stubRender(page: Page) {
  await stubApi(page, {});
  const requests: string[] = [];
  await page.route(
    (url) => url.pathname.startsWith(renderPath),
    (route) => {
      const url = new URL(route.request().url());
      requests.push(`${route.request().method()} ${url.pathname}${url.search}`);
      return route.fulfill({ json: url.pathname === renderPath ? sampleDeck : {} });
    },
  );
  return requests;
}

test('render page with lang in the fragment asks for that language', async ({ mount, page }) => {
  const requests = await stubRender(page);

  await mount(<AppRoutes />, {
    hooksConfig: { route: `/render/${sampleDeck.id}/${slide.number}#token=${TOKEN}&lang=th` },
  });

  await expect(page.getByTestId('render-slide')).toHaveAttribute('data-ready', 'true');
  expect(requests).toEqual([`POST ${renderPath}/session`, `GET ${renderPath}?language=th`]);
  expect(await page.evaluate(() => window.location.hash)).toBe('');
});

test('render page without lang sends no query', async ({ mount, page }) => {
  const requests = await stubRender(page);

  await mount(<AppRoutes />, {
    hooksConfig: { route: `/render/${sampleDeck.id}/${slide.number}#token=${TOKEN}` },
  });

  await expect(page.getByTestId('render-slide')).toHaveAttribute('data-ready', 'true');
  expect(requests).toEqual([`POST ${renderPath}/session`, `GET ${renderPath}`]);
});
