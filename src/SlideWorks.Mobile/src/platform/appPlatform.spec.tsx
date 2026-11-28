import { api } from '@slideworks/core/client';
import { CLIENT_HEADER, GUEST_HEADER, getJoin } from '@slideworks/core/guest';
import { platform } from '@slideworks/core/platform';
import { aJoinSummary } from '../test-support/builders';
import { fakeApi } from '../test-support/fakeApi';
import { renderApp } from '../test-support/renderApp';

const hosted = 'https://slideworks.app';
const other = 'https://slides.example.org';
const hostedGuest = '0123456789abcdef0123456789abcdef';

describe('platform', () => {
  it('guest headers go only to their origin', async () => {
    const server = fakeApi({ 'GET /api/join/123456': aJoinSummary() });
    await renderApp({
      api: server,
      signedIn: { origin: hosted, email: 'sam@example.com', secret: 'swd_secret' },
      guest: { origin: hosted, id: hostedGuest },
    });

    expect(platform().guestHeaders(hosted)).toEqual({
      [CLIENT_HEADER]: 'app',
      [GUEST_HEADER]: hostedGuest,
    });
    expect(platform().guestHeaders(other)).toEqual({ [CLIENT_HEADER]: 'app' });
    expect(platform().credentials()).toBe('omit');

    await getJoin(hosted, '123456');
    await getJoin(other, '123456');
    const [toHosted, toOther] = server.callsTo('GET /api/join/123456');
    expect([toHosted.origin, toOther.origin]).toEqual([hosted, other]);
    expect(toHosted.headers[GUEST_HEADER]).toBe(hostedGuest);
    expect(toOther.headers[GUEST_HEADER]).toBeUndefined();
    // A guest request never carries the bearer, even to the signed-in origin.
    expect([toHosted.headers.Authorization, toOther.headers.Authorization]).toEqual([
      undefined,
      undefined,
    ]);
    await expect(api.get('/api/join/x', { as: 'guest', origin: other })).rejects.toMatchObject({
      status: 404,
    });
  });
});
