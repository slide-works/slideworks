import { afterEach, describe, expect, it, vi } from 'vitest';
import { stubFetch } from '../test-support/stubFetch';
import { ApiError, api } from './client';

describe('api client', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('serializes the body and sets the content type on writes', async () => {
    const fetchMock = stubFetch(201, {});

    await api.post('/api/notes', { text: 'X' });

    const [, init] = fetchMock.mock.calls[0];
    expect(init.body).toBe('{"text":"X"}');
    expect(init.headers['Content-Type']).toBe('application/json');
  });

  it('surfaces the API error body message on failure', async () => {
    stubFetch(404, { error: 'Note 7 not found.' });

    await expect(api.get('/api/notes/7')).rejects.toThrow('Note 7 not found.');
  });

  it('falls back to a generic message when the error body is not JSON', async () => {
    stubFetch(500);

    await expect(api.get('/api/notes')).rejects.toThrow('Request failed (500)');
  });

  it("keeps a refusal's code, message, path and errors", async () => {
    const errors = [{ path: 'fonts[8]', code: 'too_many_fonts', message: 'At most 8 font files.' }];
    stubFetch(400, {
      error: 'manifest_invalid',
      message: 'The template manifest is not valid: fonts.',
      path: 'fonts',
      errors,
    });

    const error = await api.put('/api/templates/t1', {}).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 400,
      message: 'manifest_invalid',
      code: 'manifest_invalid',
      detail: 'The template manifest is not valid: fonts.',
      path: 'fonts',
      errors,
    });
  });

  it('leaves out an errors entry that is not a path, a code and a message', async () => {
    const entry = { path: 'purpose', code: 'manifest_invalid', message: 'Too long.' };
    stubFetch(400, { error: 'manifest_invalid', errors: [entry, 'purpose', { path: 1 }, null] });

    const error = await api.put('/api/templates/t1', {}).catch((caught: unknown) => caught);

    expect((error as ApiError).errors).toEqual([entry]);
  });

  it('carries only the code when the body has only an error', async () => {
    stubFetch(409, { error: 'stale_revision' });

    const error = (await api
      .put('/api/templates/t1', {})
      .catch((caught: unknown) => caught)) as ApiError;

    expect(error.status).toBe(409);
    expect(error.message).toBe('stale_revision');
    expect(error.code).toBe('stale_revision');
    expect(error.detail).toBeUndefined();
    expect(error.path).toBeUndefined();
    expect(error.errors).toBeUndefined();
  });

  it('carries no code, message, path or errors when the body is not JSON', async () => {
    stubFetch(500);

    const error = (await api.get('/api/notes').catch((caught: unknown) => caught)) as ApiError;

    expect(error.status).toBe(500);
    expect(error.message).toBe('Request failed (500)');
    expect(error.code).toBeUndefined();
    expect(error.detail).toBeUndefined();
    expect(error.path).toBeUndefined();
    expect(error.errors).toBeUndefined();
  });

  it('keeps an { error: "<sentence>" } body as the message, as before', async () => {
    stubFetch(404, { error: 'Note 7 not found.' });

    const error = (await api.get('/api/notes/7').catch((caught: unknown) => caught)) as ApiError;

    expect(error.status).toBe(404);
    expect(error.message).toBe('Note 7 not found.');
    expect(error.detail).toBeUndefined();
    expect(error.path).toBeUndefined();
    expect(error.errors).toBeUndefined();
  });

  it('falls back to the generic message when a JSON body has no error', async () => {
    stubFetch(400, { title: 'Bad Request' });

    await expect(api.get('/api/notes')).rejects.toThrow('Request failed (400)');
  });

  it('returns undefined for 204 responses', async () => {
    stubFetch(204);

    await expect(api.del('/api/notes/1')).resolves.toBeUndefined();
  });
});
