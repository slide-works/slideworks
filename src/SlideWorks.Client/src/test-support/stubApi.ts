import type { Page } from 'playwright/test';
import { SAMPLE_IMAGE_ID } from './fixtures/layoutSlots';

type StubbedResponse = { status?: number; json?: unknown } | 'network-down';

// Answers API calls at the network boundary in component tests, so the page
// under test makes its real requests and renders the real responses. Keys are
// paths such as '/api/me'. The sample image is always served.
export async function stubApi(page: Page, responses: Record<string, StubbedResponse>) {
  await page.route(`**/api/images/${SAMPLE_IMAGE_ID}`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
  for (const [path, response] of Object.entries(responses)) {
    await page.route(`**${path}`, (route) =>
      response === 'network-down'
        ? route.abort('connectionrefused')
        : route.fulfill({ status: response.status ?? 200, json: response.json ?? {} }),
    );
  }
}
