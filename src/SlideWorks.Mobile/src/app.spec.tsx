import { api } from '@slideworks/core/client';
import { screen, waitFor } from 'expo-router/testing-library';
import { links } from './links';
import { homeTestIds } from './screens/home/testIds';
import { talksTestIds } from './screens/talks/testIds';
import { guestKey } from './guest/guestStore';
import { SESSION_KEY } from './session/sessionStore';
import { fakeApi } from './test-support/fakeApi';
import { mockStorage } from './test-support/nativeMocks';
import { renderApp } from './test-support/renderApp';
import { signedInApi } from './test-support/signedInApi';

const origin = 'https://slideworks.app';
const signedIn = { origin, email: 'sam@example.com', secret: 'swd_secret' };
const guestId = '0123456789abcdef0123456789abcdef';

describe('app', () => {
  it('signed out lands on Home', async () => {
    await renderApp({ api: fakeApi() });

    expect(screen.getByTestId(homeTestIds.root)).toBeOnTheScreen();
  });

  it('signed out cannot open Talks', async () => {
    await renderApp({ url: links.talks(), api: fakeApi() });

    expect(screen.queryByTestId(talksTestIds.root)).toBeNull();
    expect(screen.getByTestId(homeTestIds.root)).toBeOnTheScreen();
  });

  it('signed in can open Talks', async () => {
    await renderApp({ url: links.talks(), api: signedInApi(), signedIn });

    expect(screen.getByTestId(talksTestIds.root)).toBeOnTheScreen();
  });

  it('a 401 session_ended on a signed-in request returns Home signed out and keeps the guest id', async () => {
    const server = signedInApi({
      'GET /api/decks': { status: 401, body: { error: 'session_ended' } },
    });
    await renderApp({
      url: links.talks(),
      api: server,
      signedIn,
      guest: { origin, id: guestId },
    });
    expect(screen.getByTestId(talksTestIds.root)).toBeOnTheScreen();

    await expect(api.get('/api/decks')).rejects.toMatchObject({
      status: 401,
      code: 'session_ended',
    });

    await waitFor(() => expect(screen.getByTestId(homeTestIds.root)).toBeOnTheScreen());
    expect(screen.queryByTestId(talksTestIds.root)).toBeNull();
    expect(mockStorage.secure.get(SESSION_KEY) ?? '').toBe('');
    expect(JSON.parse(mockStorage.kv.get(guestKey(origin)) ?? '{}').id).toBe(guestId);
    expect(server.callsTo('GET /api/decks')[0].headers.Authorization).toBe('Bearer swd_secret');
  });
});
