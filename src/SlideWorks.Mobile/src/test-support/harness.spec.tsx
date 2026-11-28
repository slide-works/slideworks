import { api } from '@slideworks/core/client';
import { getJoin } from '@slideworks/core/guest';
import { createHubConnection, type HubConnectionOptions } from '@slideworks/core/realtime/hub';
import { anAudienceState } from './builders';
import { fakeApi } from './fakeApi';
import { fakeHub } from './fakeHub';
import { renderApp } from './renderApp';
import { signedInApi } from './signedInApi';

const signedInOrigin = 'https://slideworks.app';
const otherOrigin = 'https://slides.example.org';

describe('fakeApi', () => {
  it('the secret only goes to the signed-in origin', async () => {
    const server = signedInApi({
      'GET /api/join/123456': { status: 404, body: { error: 'no_session' } },
    });
    await renderApp({
      api: server,
      signedIn: { origin: signedInOrigin, email: 'sam@example.com', secret: 'swd_secret' },
    });

    await api.get('/api/me');
    await expect(getJoin(otherOrigin, '123456')).rejects.toMatchObject({ code: 'no_session' });
    await expect(api.get('/api/unknown')).rejects.toMatchObject({ status: 404 });

    const [me] = server.callsTo('GET /api/me');
    expect([me.origin, me.headers.Authorization]).toEqual([signedInOrigin, 'Bearer swd_secret']);
    const [join] = server.callsTo('GET /api/join/123456');
    expect([join.origin, join.headers.Authorization]).toEqual([otherOrigin, undefined]);
    for (const call of server.calls()) {
      if (call.headers.Authorization !== undefined) expect(call.origin).toBe(signedInOrigin);
    }
  });

  it('answers unlisted routes with 404 and passes reply headers through', async () => {
    const server = fakeApi({
      'GET /api/join/654321': () => ({
        status: 200,
        body: { deckTitle: 'T', presenterName: 'P', state: anAudienceState() },
        headers: { 'X-SlideWorks-Guest': 'fedcba9876543210fedcba9876543210' },
      }),
    });

    const joined = await getJoin(otherOrigin, '654321');
    expect(joined.issuedGuestId).toBe('fedcba9876543210fedcba9876543210');
    await expect(api.get('/api/nothing')).rejects.toMatchObject({
      status: 404,
      message: 'Not found',
    });
    expect(server.callsTo('GET /api/join/654321')[0].query).toEqual({});
  });
});

describe('fakeHub', () => {
  it('records the auth it was built with', async () => {
    const hub = fakeHub();
    const secret = () => 'swd_secret';
    const connection = createHubConnection({
      auth: { kind: 'bearer', secret },
      // HttpTransportType.WebSockets (the app doesn't depend on @microsoft/signalr directly).
      transport: 1 as HubConnectionOptions['transport'],
      nextRetryDelayMs: () => null,
    });
    expect(hub.auth).toEqual({ kind: 'bearer', secret });

    hub.respond('JoinAudience', anAudienceState());
    await connection.start();
    expect(await connection.invoke('JoinAudience', '123456')).toEqual(anAudienceState());
    expect(hub.invokes).toEqual([{ method: 'JoinAudience', args: ['123456'] }]);

    const events: unknown[] = [];
    connection.on('audienceEvent', (event) => events.push(event));
    hub.emit('audienceEvent', { type: 'audienceEnded' });
    expect(events).toEqual([{ type: 'audienceEnded' }]);

    const seen: string[] = [];
    connection.onreconnecting(() => seen.push('reconnecting'));
    connection.onreconnected(() => seen.push('reconnected'));
    hub.drop();
    hub.restore();
    expect(seen).toEqual(['reconnecting', 'reconnected']);
  });
});
