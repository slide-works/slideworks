import type { Page } from 'playwright/test';
import type { Note } from '../api/types';

// Answers GET /api/notes at the network boundary, so the page under test makes
// its real request and renders the real response.
export async function stubNotes(page: Page, notes: Note[]): Promise<void> {
  await page.route('**/api/notes', (route) => route.fulfill({ json: notes }));
}
