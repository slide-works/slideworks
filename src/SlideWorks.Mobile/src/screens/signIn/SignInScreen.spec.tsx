import { api } from '@slideworks/core/client';
import { act, fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { HOSTED_SERVER, links } from '../../links';
import { LAST_SERVER_KEY, PENDING_KEY, SESSION_KEY } from '../../session/sessionStore';
import { aDeviceSessionExchange, aMe } from '../../test-support/builders';
import { signedInApi } from '../../test-support/signedInApi';
import { mockStorage, webBrowser } from '../../test-support/nativeMocks';
import { renderApp } from '../../test-support/renderApp';
import { accountTestIds } from '../account/testIds';
import { talksTestIds } from '../talks/testIds';
import { signInTestIds } from './testIds';

// expo-crypto has no Jest mock in jest-expo; Node's crypto gives the same bytes and digests.
jest.mock('expo-crypto', () => {
  // jest.mock factories are hoisted above the imports, so they load modules with require().
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const crypto = require('node:crypto') as typeof import('node:crypto');
  return {
    CryptoDigestAlgorithm: { SHA256: 'SHA-256' },
    CryptoEncoding: { BASE64: 'base64', HEX: 'hex' },
    getRandomBytes: (count: number) => new Uint8Array(crypto.randomBytes(count)),
    digestStringAsync: async (_algorithm: string, data: string) =>
      crypto.createHash('sha256').update(data).digest('base64'),
  };
});

const SECRET = 'swd_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG';
const health = { 'GET /health': { status: 200, body: { status: 'ok' } } };

function serverWith(overrides = {}) {
  return signedInApi({
    ...health,
    'POST /api/auth/device-sessions/exchange': {
      status: 201,
      body: aDeviceSessionExchange({ secret: SECRET }),
    },
    ...overrides,
  });
}

async function tapSignIn() {
  await waitFor(() => expect(screen.getByTestId(signInTestIds.signIn)).toBeEnabled());
  await fireEvent.press(screen.getByTestId(signInTestIds.signIn));
  await waitFor(() => expect(webBrowser.opened).toHaveLength(1));
  return new URL(webBrowser.opened[0].url);
}

async function redirectBack(query: Record<string, string>) {
  await act(async () => {
    webBrowser.completeAuthSession(`slideworks://signed-in?${new URLSearchParams(query)}`);
  });
}

describe('SignIn', () => {
  it('prefills the hosted server', async () => {
    const server = serverWith();
    await renderApp({ url: links.signIn(), api: server });

    expect(screen.getByTestId(signInTestIds.server)).toHaveTextContent(HOSTED_SERVER);
    await waitFor(() => expect(screen.getByTestId(signInTestIds.signIn)).toBeEnabled());
    expect(server.callsTo('GET /health').map((call) => call.origin)).toEqual([HOSTED_SERVER]);
  });

  it('remembers the last server', async () => {
    mockStorage.kv.set(LAST_SERVER_KEY, 'https://self.example');
    const server = serverWith();
    await renderApp({ url: links.signIn(), api: server });

    expect(screen.getByTestId(signInTestIds.server)).toHaveTextContent('https://self.example');
    await waitFor(() =>
      expect(server.callsTo('GET /health')[0]?.origin).toBe('https://self.example'),
    );
  });

  it('unreachable server disables sign in', async () => {
    const server = serverWith({ 'GET /health': { status: 503 } });
    await renderApp({ url: links.signIn(), api: server });

    expect(await screen.findByTestId(signInTestIds.unreachable)).toBeOnTheScreen();
    expect(screen.getByTestId(signInTestIds.signIn)).toBeDisabled();

    server.set(health);
    await fireEvent.press(screen.getByTestId(signInTestIds.useAnotherServer));
    await fireEvent.changeText(screen.getByTestId(signInTestIds.serverInput), 'other.example');
    await waitFor(() => expect(screen.getByTestId(signInTestIds.signIn)).toBeEnabled());
    expect(screen.queryByTestId(signInTestIds.unreachable)).toBeNull();
  });

  it('completes the handoff and lands on Talks', async () => {
    const server = serverWith();
    await renderApp({ url: links.signIn(), api: server });

    const opened = await tapSignIn();
    expect(opened.origin + opened.pathname).toBe(`${HOSTED_SERVER}/app/sign-in`);
    expect(webBrowser.opened[0].redirectUrl).toBe('slideworks://signed-in');
    const state = opened.searchParams.get('state') ?? '';
    await redirectBack({ code: 'c0de', state, iss: HOSTED_SERVER });

    await waitFor(() => expect(screen.getByTestId(talksTestIds.root)).toBeOnTheScreen());
    const [exchange] = server.callsTo('POST /api/auth/device-sessions/exchange');
    expect(exchange.body).toMatchObject({ code: 'c0de' });
    expect(exchange.headers.Authorization).toBeUndefined();
    expect(JSON.parse(mockStorage.secure.get(SESSION_KEY) ?? '{}')).toEqual({
      origin: HOSTED_SERVER,
      secret: SECRET,
    });
  });

  it('cancelled handoff stays on sign in quietly', async () => {
    const server = serverWith();
    await renderApp({ url: links.signIn(), api: server });

    const state = (await tapSignIn()).searchParams.get('state') ?? '';
    await redirectBack({ error: 'cancelled', state, iss: HOSTED_SERVER });

    expect(screen.getByTestId(signInTestIds.root)).toBeOnTheScreen();
    await waitFor(() => expect(screen.getByTestId(signInTestIds.signIn)).toBeEnabled());
    expect(screen.queryByTestId(signInTestIds.error)).toBeNull();
    expect(server.callsTo('POST /api/auth/device-sessions/exchange')).toHaveLength(0);

    await fireEvent.press(screen.getByTestId(signInTestIds.signIn));
    await waitFor(() => expect(webBrowser.opened).toHaveLength(2));
    await act(async () => webBrowser.cancelAuthSession());
    expect(screen.queryByTestId(signInTestIds.error)).toBeNull();
  });

  it('wrong iss never calls exchange', async () => {
    const server = serverWith();
    await renderApp({ url: links.signIn(), api: server });

    const state = (await tapSignIn()).searchParams.get('state') ?? '';
    await redirectBack({ code: 'c0de', state, iss: 'https://evil.example' });

    expect(await screen.findByTestId(signInTestIds.error)).toBeOnTheScreen();
    expect(server.callsTo('POST /api/auth/device-sessions/exchange')).toHaveLength(0);
    expect(mockStorage.secure.get(SESSION_KEY)).toBeUndefined();
  });

  it("shows the server's refusal message", async () => {
    const message = 'Sign-in expired. Try again.';
    const server = serverWith({
      'POST /api/auth/device-sessions/exchange': {
        status: 400,
        body: { error: 'sign_in_expired', message },
      },
    });
    await renderApp({ url: links.signIn(), api: server });

    const state = (await tapSignIn()).searchParams.get('state') ?? '';
    await redirectBack({ code: 'c0de', state, iss: HOSTED_SERVER });

    expect(await screen.findByTestId(signInTestIds.error)).toHaveTextContent(message);
    expect(screen.getByTestId(signInTestIds.root)).toBeOnTheScreen();
    expect(mockStorage.secure.get(SESSION_KEY)).toBeUndefined();
  });

  it('pending person sees the waiting message', async () => {
    const me = aMe({ access: 'pending' });
    const server = serverWith({
      'GET /api/me': me,
      'POST /api/auth/device-sessions/exchange': {
        status: 201,
        body: aDeviceSessionExchange({ secret: SECRET, me }),
      },
    });
    await renderApp({ url: links.signIn(), api: server });

    const state = (await tapSignIn()).searchParams.get('state') ?? '';
    await redirectBack({ code: 'c0de', state, iss: HOSTED_SERVER });

    expect(await screen.findByTestId(accountTestIds.pending)).toBeOnTheScreen();
    expect(mockStorage.kv.get(PENDING_KEY)).toBe('true');
    expect(JSON.parse(mockStorage.secure.get(SESSION_KEY) ?? '{}').secret).toBe(SECRET);
  });

  it('exchange and later calls go only to the typed origin', async () => {
    const typed = 'http://10.0.2.2:5510';
    const server = serverWith();
    await renderApp({ url: links.signIn(), api: server });

    await fireEvent.press(screen.getByTestId(signInTestIds.useAnotherServer));
    await fireEvent.changeText(screen.getByTestId(signInTestIds.serverInput), `${typed}/`);
    const opened = await tapSignIn();
    expect(opened.origin).toBe(typed);
    await redirectBack({ code: 'c0de', state: opened.searchParams.get('state') ?? '', iss: typed });
    await waitFor(() => expect(screen.getByTestId(talksTestIds.root)).toBeOnTheScreen());

    await api.get('/api/decks');
    const signedInCalls = server.calls().filter((call) => call.path !== '/health');
    expect(signedInCalls.map((call) => call.path)).toContain('/api/auth/device-sessions/exchange');
    expect(signedInCalls.map((call) => call.path)).toContain('/api/decks');
    expect(new Set(signedInCalls.map((call) => call.origin))).toEqual(new Set([typed]));
    for (const call of server.calls().filter((c) => c.origin !== typed)) {
      expect(call.headers.Authorization).toBeUndefined();
    }
    expect(server.callsTo('GET /api/decks').at(-1)?.headers.Authorization).toBe(`Bearer ${SECRET}`);
    expect(mockStorage.kv.get(LAST_SERVER_KEY)).toBe(typed);
  });
});
