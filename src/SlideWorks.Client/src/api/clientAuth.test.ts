import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { stubFetch } from '../test-support/stubFetch';
import { ApiError, api, clearToken, getToken, setOnUnauthorized, setToken } from './client';

describe('api client token handling', () => {
  beforeEach(() => setToken('secret-token'));

  afterEach(() => {
    clearToken();
    vi.unstubAllGlobals();
  });

  it('attaches the stored token as a bearer header', async () => {
    const fetchMock = stubFetch(200, []);

    await api.get('/api/notes');

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers.Authorization).toBe('Bearer secret-token');
  });

  it('clears the token and calls the unauthorized handler on 401', async () => {
    stubFetch(401);
    const onUnauthorized = vi.fn();
    setOnUnauthorized(onUnauthorized);

    await expect(api.get('/api/notes')).rejects.toThrow(ApiError);

    expect(getToken()).toBeNull();
    expect(onUnauthorized).toHaveBeenCalled();
  });
});
