import { afterEach, describe, expect, it, vi } from 'vitest';
import { stubFetch } from '../test-support/stubFetch';
import { ApiError, api, redirectToSignIn, setOnUnauthorized } from './client';

describe('api client session handling', () => {
  afterEach(() => {
    setOnUnauthorized(() => undefined);
    vi.unstubAllGlobals();
  });

  it('sends the session cookie same-origin and no token or origin headers of its own', async () => {
    const fetchMock = stubFetch(200, []);

    await api.post('/api/decks', { title: 'X' });

    const [, init] = fetchMock.mock.calls[0];
    expect(init.credentials).toBe('same-origin');
    expect(Object.keys(init.headers)).toEqual(['Content-Type']);
  });

  it('calls the unauthorized handler on a 401 mid-session', async () => {
    stubFetch(401, { error: 'Not signed in' });
    const onUnauthorized = vi.fn();
    setOnUnauthorized(onUnauthorized);

    const error = await api.get('/api/decks').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it('leaves the 401 of the session check to its caller', async () => {
    stubFetch(401, { error: 'Not signed in' });
    const onUnauthorized = vi.fn();
    setOnUnauthorized(onUnauthorized);

    const error = await api.get('/api/me').catch((e: unknown) => e);

    expect((error as ApiError).status).toBe(401);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('sends the person to sign in and back to the same path, query and hash', () => {
    const assign = vi.fn();

    redirectToSignIn({ pathname: '/decks/abc', search: '?tab=sharing', hash: '#top', assign });

    expect(assign).toHaveBeenCalledExactlyOnceWith(
      `/signin?returnTo=${encodeURIComponent('/decks/abc?tab=sharing#top')}`,
    );
  });

  it('stays on the sign-in page instead of nesting returnTo or looping', () => {
    const assign = vi.fn();

    redirectToSignIn({ pathname: '/signin', search: '?returnTo=%2Fdecks', hash: '', assign });

    expect(assign).not.toHaveBeenCalled();
  });
});
