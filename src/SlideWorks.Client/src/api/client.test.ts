import { afterEach, describe, expect, it, vi } from 'vitest';
import { stubFetch } from '../test-support/stubFetch';
import { api } from './client';

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

  it('returns undefined for 204 responses', async () => {
    stubFetch(204);

    await expect(api.del('/api/notes/1')).resolves.toBeUndefined();
  });
});
