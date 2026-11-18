import { test, expect } from '@playwright/experimental-ct-react';
import { templateEditor } from '../test-support/fixtures/templates';
import { stubTemplatesApi } from '../test-support/stubTemplatesApi';
import { TemplateDraftHarness } from './testing/TemplateDraftHarness';

const id = templateEditor.id;

// What the server answers to a save: the next revision, with the purpose changed.
const saved = {
  ...templateEditor,
  revision: templateEditor.revision + 1,
  manifest: { ...templateEditor.manifest, purpose: 'A new purpose.' },
};

test('loads the draft', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page);
  await mount(<TemplateDraftHarness id={id} />);

  await expect(page.getByTestId('draft-state')).toHaveText('loaded');
  await expect(page.getByTestId('draft-revision')).toHaveText('7');
  await expect(page.getByTestId('draft-purpose')).toHaveText(templateEditor.manifest.purpose);
  expect(requests).toEqual([{ call: 'get', id, body: null }]);
});

test('shows a message when the draft cannot be read', async ({ mount, page }) => {
  await stubTemplatesApi(page, { get: { status: 404, json: { error: 'No such template.' } } });
  await mount(<TemplateDraftHarness id={id} />);

  await expect(page.getByTestId('draft-state')).toHaveText('failed');
  await expect(page.getByTestId('draft-load-error')).toHaveText('No such template.');
});

test('a change saves the whole manifest with the revision it read', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page, { save: { json: saved } });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change purpose' }).click();

  await expect(page.getByTestId('draft-revision')).toHaveText('8');
  await expect(page.getByTestId('draft-purpose')).toHaveText('A new purpose.');
  await expect(page.getByTestId('draft-saving')).toHaveText('false');
  await expect(page.getByTestId('draft-save-message')).toHaveText('');
  expect(requests[1]).toEqual({
    call: 'save',
    id,
    body: { manifest: { ...templateEditor.manifest, purpose: 'A new purpose.' }, revision: 7 },
  });
  expect(requests).toHaveLength(2);
});

test('two changes in a row save one after the other, the second on the first answer', async ({
  mount,
  page,
}) => {
  const requests = await stubTemplatesApi(page, { save: { json: saved } });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change twice' }).click();

  await expect.poll(() => requests.length).toBe(3);
  await expect(page.getByTestId('draft-saving')).toHaveText('false');
  expect(requests[1].body).toEqual({
    manifest: { ...templateEditor.manifest, purpose: 'A new purpose.' },
    revision: 7,
  });
  expect(requests[2].body).toEqual({
    manifest: { ...saved.manifest, lockPalette: false },
    revision: 8,
  });
});

test('a refused save keeps the draft and reports the code', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    save: {
      status: 409,
      json: {
        error: 'stale_revision',
        message: 'This template has changed since you read it. Read it again, then retry.',
        errors: [],
      },
    },
  });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change purpose' }).click();

  await expect(page.getByTestId('draft-save-code')).toHaveText('stale_revision');
  await expect(page.getByTestId('draft-save-message')).not.toHaveText('');
  await expect(page.getByTestId('draft-saving')).toHaveText('false');
  await expect(page.getByTestId('draft-revision')).toHaveText('7');
  await expect(page.getByTestId('draft-purpose')).toHaveText(templateEditor.manifest.purpose);
});

test('a save that cannot reach the server reports it without a code', async ({ mount, page }) => {
  await stubTemplatesApi(page, { save: 'network-down' });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change purpose' }).click();

  await expect(page.getByTestId('draft-save-message')).toHaveText("Can't reach SlideWorks.");
  await expect(page.getByTestId('draft-save-code')).toHaveText('');
});

test('reload reads the draft again', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page);
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Reload' }).click();

  await expect.poll(() => requests.map((request) => request.call)).toEqual(['get', 'get']);
  await expect(page.getByTestId('draft-state')).toHaveText('loaded');
});
