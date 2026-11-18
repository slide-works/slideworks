import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
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
  await expect(page.getByTestId('draft-outcome')).toHaveText('{"ok":true}');
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

// What a change resolved to, as the harness shows it.
const outcome = (page: Page) =>
  page
    .getByTestId('draft-outcome')
    .textContent()
    .then((text) => (text ? (JSON.parse(text) as unknown) : null));

test("a refused change resolves not ok with the server's message and path", async ({
  mount,
  page,
}) => {
  const message = 'The template manifest is not valid: purpose is too long.';
  const requests = await stubTemplatesApi(page, {
    save: {
      status: 400,
      json: {
        error: 'manifest_invalid',
        message,
        path: 'purpose',
        errors: [{ path: 'purpose', code: 'manifest_invalid', message }],
      },
    },
  });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change purpose' }).click();

  await expect
    .poll(() => outcome(page))
    .toEqual({ ok: false, code: 'manifest_invalid', message, path: 'purpose' });
  await expect(page.getByTestId('draft-save-code')).toHaveText('manifest_invalid');
  await expect(page.getByTestId('draft-save-message')).toHaveText(message);
  await expect(page.getByTestId('draft-saving')).toHaveText('false');
  await expect(page.getByTestId('draft-revision')).toHaveText('7');
  await expect(page.getByTestId('draft-purpose')).toHaveText(templateEditor.manifest.purpose);
  // Nothing is read again after a refusal that is not a stale revision.
  expect(requests.map((request) => request.call)).toEqual(['get', 'save']);
});

test('a stale revision reads the draft again once and reports it', async ({ mount, page }) => {
  const theirs = {
    ...templateEditor,
    revision: 9,
    manifest: { ...templateEditor.manifest, purpose: 'Changed somewhere else.' },
  };
  const requests = await stubTemplatesApi(page, {
    get: [{ json: templateEditor }, { json: theirs }],
    save: [
      {
        status: 409,
        json: {
          error: 'stale_revision',
          message: 'This template has changed since you read it. Read it again, then retry.',
          errors: [],
        },
      },
      { json: { ...theirs, revision: 10 } },
    ],
  });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change purpose' }).click();

  await expect
    .poll(() => outcome(page))
    .toEqual({
      ok: false,
      code: 'stale_revision',
      message:
        'This template was changed somewhere else, so it has been read again. Check it, then make your change again.',
    });
  await expect(page.getByTestId('draft-revision')).toHaveText('9');
  await expect(page.getByTestId('draft-purpose')).toHaveText('Changed somewhere else.');
  await expect(page.getByTestId('draft-save-code')).toHaveText('stale_revision');
  await expect(page.getByTestId('draft-saving')).toHaveText('false');
  expect(requests.map((request) => request.call)).toEqual(['get', 'save', 'get']);

  // The next change saves on top of the revision that was read again.
  await page.getByRole('button', { name: 'Change purpose' }).click();
  await expect(page.getByTestId('draft-revision')).toHaveText('10');
  expect(requests[3]).toMatchObject({ call: 'save', body: { revision: 9 } });
  expect(requests).toHaveLength(4);
});

test('a change queued behind a stale revision is dropped, not saved on the re-read draft', async ({
  mount,
  page,
}) => {
  const theirs = {
    ...templateEditor,
    revision: 9,
    manifest: { ...templateEditor.manifest, purpose: 'Changed somewhere else.' },
  };
  const requests = await stubTemplatesApi(page, {
    get: [{ json: templateEditor }, { json: theirs }],
    save: { status: 409, json: { error: 'stale_revision', message: 'Read it again.' } },
  });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change twice' }).click();

  // The second change resolved last: it was dropped and says so.
  await expect.poll(() => outcome(page)).toMatchObject({ ok: false, code: 'stale_revision' });
  await expect(page.getByTestId('draft-saving')).toHaveText('false');
  await expect(page.getByTestId('draft-revision')).toHaveText('9');
  expect(requests.map((request) => request.call)).toEqual(['get', 'save', 'get']);
});

test('a stale revision that cannot be read again says so', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    get: [{ json: templateEditor }, 'network-down'],
    save: { status: 409, json: { error: 'stale_revision', message: 'Read it again.' } },
  });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change purpose' }).click();

  await expect
    .poll(() => outcome(page))
    .toEqual({
      ok: false,
      code: 'stale_revision',
      message:
        'This template was changed somewhere else and could not be read again. Reload the page, then make your change again.',
    });
  await expect(page.getByTestId('draft-revision')).toHaveText('7');
});

test('a refused save stays reported when a queued save after it succeeds', async ({
  mount,
  page,
}) => {
  const refusal = {
    status: 400,
    json: { error: 'manifest_invalid', message: 'The template manifest is not valid: purpose.' },
  };
  const requests = await stubTemplatesApi(page, {
    save: [refusal, { json: { ...saved, manifest: templateEditor.manifest } }, { json: saved }],
  });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change twice' }).click();

  // The second save went through, and the first one's refusal is still on screen.
  await expect(page.getByTestId('draft-revision')).toHaveText('8');
  await expect(page.getByTestId('draft-saving')).toHaveText('false');
  await expect(page.getByTestId('draft-save-code')).toHaveText('manifest_invalid');
  await expect(page.getByTestId('draft-purpose')).toHaveText(templateEditor.manifest.purpose);
  expect(requests).toHaveLength(3);

  // Changing that field again takes the message away.
  await page.getByRole('button', { name: 'Change purpose' }).click();
  await expect(page.getByTestId('draft-purpose')).toHaveText('A new purpose.');
  await expect(page.getByTestId('draft-save-code')).toHaveText('');
});

test('a save that cannot reach the server reports it without a code', async ({ mount, page }) => {
  await stubTemplatesApi(page, { save: 'network-down' });
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Change purpose' }).click();

  await expect(page.getByTestId('draft-save-message')).toHaveText("Can't reach SlideWorks.");
  await expect(page.getByTestId('draft-save-code')).toHaveText('');
  expect(await outcome(page)).toEqual({
    ok: false,
    code: null,
    message: "Can't reach SlideWorks.",
  });
});

test('reload reads the draft again', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page);
  await mount(<TemplateDraftHarness id={id} />);
  await expect(page.getByTestId('draft-revision')).toHaveText('7');

  await page.getByRole('button', { name: 'Reload' }).click();

  await expect.poll(() => requests.map((request) => request.call)).toEqual(['get', 'get']);
  await expect(page.getByTestId('draft-state')).toHaveText('loaded');
});
