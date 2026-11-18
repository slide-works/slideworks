import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { me } from '../../test-support/fixtures/screens';
import { editorTabProps, templateEditor } from '../../test-support/fixtures/templates';
import { stubApi } from '../../test-support/stubApi';
import { stubTemplatesApi } from '../../test-support/stubTemplatesApi';
import { TEMPLATES_TEST_IDS, editorTabButtonTestId } from '../testIds';
import type { TemplateEditorView } from '../types';
import { PublishChecklist } from './PublishChecklist';

const READY: TemplateEditorView = {
  ...templateEditor,
  canPublish: true,
  checklist: templateEditor.checklist.map((row) => ({ ...row, state: 'pass', detail: '' })),
};
const NEXT = templateEditor.version + 1;

test('publish is off until every row passes', async ({ mount, page }) => {
  await mount(<PublishChecklist {...editorTabProps()} />);
  await expect(page.getByTestId('checklist-count')).toHaveText('2 to fix');
  const button = page.getByRole('button', { name: `Publish version ${NEXT}` });
  await expect(button).toBeDisabled();
  await expect(button).toHaveAccessibleDescription('Fix 2 checks to publish');
  await expect(page.getByTestId('checklist-row')).toHaveCount(templateEditor.checklist.length);
  await expect(page.getByRole('link', { name: /^Fix / })).toHaveCount(2);
});

test('publish is on when the server says so', async ({ mount, page }) => {
  await mount(<PublishChecklist {...editorTabProps({ draft: READY })} />);
  await expect(page.getByTestId('checklist-count')).toHaveText('All clear');
  await expect(page.getByRole('button', { name: `Publish version ${NEXT}` })).toBeEnabled();
});

test('opens the tab a Fix link names', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await stubTemplatesApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: `/templates/${templateEditor.id}/edit` } });
  await expect(page.getByTestId(editorTabButtonTestId('basics'))).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await page
    .getByTestId(TEMPLATES_TEST_IDS.checklist)
    .getByRole('link', { name: 'Fix Font rights confirmed' })
    .click();
  await expect(page.getByTestId(editorTabButtonTestId('fonts'))).toHaveAttribute(
    'aria-selected',
    'true',
  );
});

test('offers Update my decks after publishing', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page, {
    publish: { json: { version: NEXT, decksBehind: 3 } },
    updateMyDecks: { json: { count: 3 } },
  });
  let reloads = 0;
  await mount(
    <PublishChecklist
      {...editorTabProps({
        draft: READY,
        reload: () => {
          reloads += 1;
          return Promise.resolve();
        },
      })}
    />,
  );
  await page.getByRole('button', { name: `Publish version ${NEXT}` }).click();
  const message = page.getByTestId('publish-message');
  await expect(message).toContainText(`Version ${NEXT} published.`);
  await expect(message).toContainText('3 of your decks are on an older version.');
  await expect(message).toBeFocused();
  expect(reloads).toBe(1);

  await page.getByRole('button', { name: 'Update my 3 decks' }).click();
  await expect(message).toHaveText('3 decks updated.');
  await expect(message).toBeFocused();
  await expect(page.getByRole('button', { name: 'Update my 3 decks' })).toHaveCount(0);
  expect(requests.map((request) => request.call)).toEqual(['publish', 'updateMyDecks']);
  expect(requests.every((request) => request.id === READY.id)).toBe(true);
});

test('Not now closes the offer', async ({ mount, page }) => {
  await stubTemplatesApi(page, { publish: { json: { version: NEXT, decksBehind: 1 } } });
  await mount(<PublishChecklist {...editorTabProps({ draft: READY })} />);
  await page.getByRole('button', { name: `Publish version ${NEXT}` }).click();
  await expect(page.getByTestId('publish-message')).toContainText('1 of your decks is on an older');
  await page.getByRole('button', { name: 'Not now' }).click();
  await expect(page.getByRole('button', { name: 'Update my 1 deck' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: `Publish version ${NEXT}` })).toBeFocused();
});

test('reports a failed update', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    publish: { json: { version: NEXT, decksBehind: 2 } },
    updateMyDecks: [
      { status: 400, json: { error: 'no_template', message: 'No template to update.' } },
      { status: 500 },
    ],
  });
  await mount(<PublishChecklist {...editorTabProps({ draft: READY })} />);
  await page.getByRole('button', { name: `Publish version ${NEXT}` }).click();
  await page.getByRole('button', { name: 'Update my 2 decks' }).click();
  await expect(page.getByRole('alert')).toHaveText('No template to update.');
  await page.getByRole('button', { name: 'Update my 2 decks' }).click();
  await expect(page.getByRole('alert')).toHaveText('Could not update your decks. Try again.');
  await expect(page.getByRole('button', { name: 'Update my 2 decks' })).toBeVisible();
});

test("shows the server's refusal and focuses the failing row", async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    publish: {
      status: 400,
      json: { error: 'rights_not_confirmed', message: 'Confirm you can serve these fonts.' },
    },
  });
  // The server's checklist, as it reads when the refusal comes: one row still to do.
  const draft: TemplateEditorView = {
    ...READY,
    checklist: READY.checklist.map((row) =>
      row.key === 'fontRights' ? { ...row, state: 'todo' } : row,
    ),
  };
  await mount(<PublishChecklist {...editorTabProps({ draft })} />);
  await page.getByRole('button', { name: `Publish version ${NEXT}` }).click();
  const alert = page.getByRole('alert');
  await expect(alert).toContainText('Confirm you can serve these fonts.');
  await expect(alert.getByRole('link', { name: 'Go to the Fonts tab' })).toBeVisible();
  await expect(page.locator('[data-key="fontRights"]')).toBeFocused();
});

test('a network failure changes nothing', async ({ mount, page }) => {
  await stubTemplatesApi(page, { publish: 'network-down' });
  await mount(<PublishChecklist {...editorTabProps({ draft: READY })} />);
  await page.getByRole('button', { name: `Publish version ${NEXT}` }).click();
  await expect(page.getByRole('alert')).toHaveText('Could not publish. Nothing was changed.');
  await expect(page.getByRole('button', { name: `Publish version ${NEXT}` })).toBeEnabled();
});

test('makes no offer when no deck is behind', async ({ mount, page }) => {
  await stubTemplatesApi(page, { publish: { json: { version: NEXT, decksBehind: 0 } } });
  await mount(<PublishChecklist {...editorTabProps({ draft: READY })} />);
  await page.getByRole('button', { name: `Publish version ${NEXT}` }).click();
  await expect(page.getByTestId('publish-message')).toHaveText(`Version ${NEXT} published.`);
  await expect(page.getByRole('button', { name: /^Update my/ })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Not now' })).toHaveCount(0);
});

test('screenshot: checklist with two to fix', async ({ mount, page }) => {
  await mount(<PublishChecklist {...editorTabProps()} />);
  await expect(page.getByTestId('checklist-count')).toHaveText('2 to fix');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.checklist)).toHaveScreenshot(
    'checklist-two-to-fix.png',
  );
});
