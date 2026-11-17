import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { NotesText } from './NotesText';

// NotesText renders a fragment, so look inside the mount root.
const root = (page: Page) => page.locator('#root');

test('NotesText shows bold text and "- " lines as a list, as typed otherwise', async ({
  mount,
  page,
}) => {
  await mount(
    <NotesText
      text={
        'Say: ask the room for a **story**.\n- **Customer first**, never the layer\n- Keep each slice under a week\nThen move on.'
      }
    />,
  );

  await expect(root(page).locator('p')).toHaveText([
    'Say: ask the room for a story.',
    'Then move on.',
  ]);
  await expect(root(page).locator('b')).toHaveText(['story', 'Customer first']);
  await expect(root(page).locator('ul > li')).toHaveText([
    'Customer first, never the layer',
    'Keep each slice under a week',
  ]);
});

test('NotesText never renders HTML, and keeps an unpaired marker as typed', async ({
  mount,
  page,
}) => {
  await mount(<NotesText text={'<img src=x onerror=alert(1)> and a **lone marker'} />);

  await expect(root(page).locator('img')).toHaveCount(0);
  await expect(root(page).locator('b')).toHaveCount(0);
  await expect(root(page).locator('p')).toHaveText(
    '<img src=x onerror=alert(1)> and a **lone marker',
  );
});

test('NotesText skips blank lines and starts a new list after other text', async ({
  mount,
  page,
}) => {
  await mount(<NotesText text={'- one\n\n- two\nmiddle\n- three'} />);

  await expect(root(page).locator('ul')).toHaveCount(3);
  await expect(root(page).locator('p')).toHaveText(['middle']);
});
