import type { Page } from 'playwright/test';

// The App shell redirects to /login when localStorage has no API token, so
// page-level tests store one before mounting. Key mirrors src/api/client.ts.
export async function seedApiToken(page: Page): Promise<void> {
  await page.evaluate(() => localStorage.setItem('slide-works-api-token', 'component-test-token'));
}
