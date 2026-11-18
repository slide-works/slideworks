import { test, expect } from '@playwright/experimental-ct-react';
import { deckPage } from '../../test-support/fixtures/screens';
import { SelectedSlide } from './SelectedSlide';

const withNotes = (notes: string | null) => ({ ...deckPage.slides[0], notes });

test('bold and list marks render as in the presenter', async ({ mount, page }) => {
  await mount(<SelectedSlide slide={withNotes('**Say:** hello\n- first\n- second')} />);
  const notes = page.getByTestId('selected-notes');
  await expect(notes.locator('b')).toHaveCount(1);
  await expect(notes.locator('b')).toHaveText('Say:');
  await expect(notes.locator('ul > li')).toHaveText(['first', 'second']);
});

test('an unpaired mark is shown as typed', async ({ mount, page }) => {
  await mount(<SelectedSlide slide={withNotes('a **lone marker')} />);
  const notes = page.getByTestId('selected-notes');
  await expect(notes.locator('b')).toHaveCount(0);
  await expect(notes).toHaveText('a **lone marker');
});

test('a slide with no notes says so', async ({ mount, page }) => {
  await mount(<SelectedSlide slide={withNotes(null)} />);
  await expect(page.getByTestId('selected-notes')).toHaveText('No speaker notes.');
});

test('markup in notes is shown as text', async ({ mount, page }) => {
  await mount(<SelectedSlide slide={withNotes('<b>x</b>')} />);
  const notes = page.getByTestId('selected-notes');
  await expect(notes).toHaveText('<b>x</b>');
  await expect(notes.locator('b')).toHaveCount(0);
});

test('screenshot: selected slide notes with bold', async ({ mount }) => {
  const component = await mount(
    <SelectedSlide slide={withNotes('**Say:** hello\n- first\n- second')} />,
  );
  await expect(component).toHaveScreenshot();
});
