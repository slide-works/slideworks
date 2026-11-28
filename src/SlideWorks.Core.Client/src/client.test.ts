import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, api } from './client';
import { configureTestPlatform, stubFetch } from './test-support/testPlatform';

afterEach(() => vi.unstubAllGlobals());

const failureOf = (promise: Promise<unknown>) => promise.catch((caught: unknown) => caught);

describe('client', () => {
  it('person 401 session_ended calls onUnauthorized with session_ended', async () => {
    const { onUnauthorized } = configureTestPlatform();
    stubFetch(401, { error: 'session_ended' });

    const error = await failureOf(api.get('/api/decks'));

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    expect(onUnauthorized).toHaveBeenCalledExactlyOnceWith('session_ended');
  });

  it('person 401 without body calls onUnauthorized with unauthorized', async () => {
    const { onUnauthorized } = configureTestPlatform();
    stubFetch(401);

    const error = await failureOf(api.post('/api/decks', { title: 'X' }));

    expect((error as ApiError).message).toBe('Request failed (401)');
    expect(onUnauthorized).toHaveBeenCalledExactlyOnceWith('unauthorized');
  });

  it('401 on /api/me never calls onUnauthorized', async () => {
    const { onUnauthorized } = configureTestPlatform();
    stubFetch(401, { error: 'session_ended' });

    const error = await failureOf(api.get('/api/me'));

    expect((error as ApiError).status).toBe(401);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('guest 401 never calls onUnauthorized', async () => {
    const { onUnauthorized } = configureTestPlatform();
    stubFetch(401, { error: 'session_ended' });

    const error = await failureOf(
      api.get('/api/guest/polls', { as: 'guest', origin: 'https://talk.example' }),
    );

    expect((error as ApiError).status).toBe(401);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('guest 403 pending_approval does not call onPendingApproval', async () => {
    const { onPendingApproval } = configureTestPlatform();
    stubFetch(403, { error: 'pending_approval' });

    const error = await failureOf(
      api.get('/api/guest/polls', { as: 'guest', origin: 'https://talk.example' }),
    );

    expect((error as ApiError).status).toBe(403);
    expect(onPendingApproval).not.toHaveBeenCalled();
  });

  it('a guest request without an origin throws before fetching', async () => {
    configureTestPlatform();
    const fetchMock = stubFetch(200, {});

    // @ts-expect-error a guest request must name its origin
    await expect(api.get('/api/join/1', { as: 'guest' })).rejects.toThrow(/origin/);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('a person request goes to apiOrigin even when an origin is passed', async () => {
    configureTestPlatform();
    const fetchMock = stubFetch(200, {});

    // @ts-expect-error a person request cannot name an origin
    await api.get('/api/decks', { origin: 'https://evil.example' });

    expect(fetchMock.mock.calls[0][0]).toBe('https://signed-in.example/api/decks');
  });

  it('pending 403 calls onPendingApproval', async () => {
    const { onPendingApproval, onUnauthorized } = configureTestPlatform();
    stubFetch(403, { error: 'pending_approval' });

    const error = await failureOf(api.get('/api/decks'));

    expect((error as ApiError).status).toBe(403);
    expect(onPendingApproval).toHaveBeenCalledOnce();
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('other 403 and a 403 on /api/me do not call onPendingApproval', async () => {
    const { onPendingApproval } = configureTestPlatform();
    stubFetch(403, { error: 'Admins only' });
    await failureOf(api.get('/api/admin/users'));
    stubFetch(403, { error: 'pending_approval' });
    await failureOf(api.get('/api/me'));

    expect(onPendingApproval).not.toHaveBeenCalled();
  });

  it('sends authHeaders to apiOrigin for person requests and guestHeaders to the given origin for guest requests', async () => {
    configureTestPlatform();
    const fetchMock = stubFetch(200, {});

    await api.put('/api/decks/d1', { title: 'X' });
    await api.get('/api/join/123456', { as: 'guest', origin: 'https://talk.example' });

    const [[personUrl, person], [guestUrl, guest]] = fetchMock.mock.calls;
    expect(personUrl).toBe('https://signed-in.example/api/decks/d1');
    expect(person.headers).toEqual({
      'Content-Type': 'application/json',
      Authorization: 'Bearer swd_secret',
    });
    expect(person.credentials).toBe('omit');
    expect(guestUrl).toBe('https://talk.example/api/join/123456');
    expect(guest.headers).toEqual({
      'X-SlideWorks-Client': 'app',
      'X-SlideWorks-Guest': 'id-for-https://talk.example',
    });
    expect(guest.credentials).toBe('omit');
  });

  it("keeps a refusal's code, message, path and errors", async () => {
    configureTestPlatform();
    const errors = [{ path: 'deviceName', code: 'too_long', message: 'At most 80 characters.' }];
    stubFetch(400, {
      error: 'invalid_request',
      message: 'deviceName is too long.',
      path: 'deviceName',
      errors,
    });

    const error = (await failureOf(api.post('/api/x', {}))) as ApiError;

    expect(error.message).toBe('invalid_request');
    expect(error.code).toBe('invalid_request');
    expect(error.detail).toBe('deviceName is too long.');
    expect(error.path).toBe('deviceName');
    expect(error.errors).toEqual(errors);
  });

  it('resolves a 204 as undefined', async () => {
    configureTestPlatform();
    stubFetch(204);

    await expect(api.del('/api/decks/d1')).resolves.toBeUndefined();
  });
});
