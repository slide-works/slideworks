import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { guestKey } from '../../guest/guestStore';
import { links } from '../../links';
import { LAST_SERVER_KEY, SESSION_KEY } from '../../session/sessionStore';
import { aMe } from '../../test-support/builders';
import { APP_BUILD, APP_VERSION, mockStorage } from '../../test-support/nativeMocks';
import { renderApp } from '../../test-support/renderApp';
import { signedInApi } from '../../test-support/signedInApi';
import { homeTestIds } from '../home/testIds';
import { accountTestIds } from './testIds';

const origin = 'https://self.example';
const signedIn = { origin, email: 'sam@example.com', secret: 'swd_secret' };
const guestId = '0123456789abcdef0123456789abcdef';
const me = aMe({ name: 'Sam Speaker', email: 'sam@example.com' });

describe('Account', () => {
  it('shows version and build', async () => {
    await renderApp({ url: links.account(), api: signedInApi({ 'GET /api/me': me }), signedIn });

    expect(screen.getByTestId(accountTestIds.version)).toHaveTextContent(
      new RegExp(`${APP_VERSION}.*\\(${APP_BUILD}\\)`),
    );
    expect(screen.getByTestId(accountTestIds.server)).toHaveTextContent(origin);
    await waitFor(() => expect(screen.getByTestId(accountTestIds.name)).toHaveTextContent(me.name));
    expect(screen.getByTestId(accountTestIds.email)).toHaveTextContent(me.email);
    expect(screen.queryByTestId(accountTestIds.pending)).toBeNull();
  });

  it('sign out asks first and returns Home keeping the guest id', async () => {
    const server = signedInApi({
      'GET /api/me': me,
      'DELETE /api/auth/device-sessions/current': { status: 204 },
    });
    await renderApp({
      url: links.account(),
      api: server,
      signedIn,
      guest: { origin, id: guestId },
    });

    await fireEvent.press(screen.getByTestId(accountTestIds.signOut));
    expect(screen.getByTestId(accountTestIds.confirmSignOut)).toBeOnTheScreen();
    expect(server.callsTo('DELETE /api/auth/device-sessions/current')).toHaveLength(0);
    await fireEvent.press(screen.getByTestId(accountTestIds.cancelSignOut));
    expect(screen.getByTestId(accountTestIds.root)).toBeOnTheScreen();
    expect(server.callsTo('DELETE /api/auth/device-sessions/current')).toHaveLength(0);

    await fireEvent.press(screen.getByTestId(accountTestIds.signOut));
    await fireEvent.press(screen.getByTestId(accountTestIds.confirmSignOut));

    await waitFor(() => expect(screen.getByTestId(homeTestIds.root)).toBeOnTheScreen());
    const [revoke] = server.callsTo('DELETE /api/auth/device-sessions/current');
    expect(revoke.origin).toBe(origin);
    expect(revoke.headers.Authorization).toBe('Bearer swd_secret');
    expect(mockStorage.secure.get(SESSION_KEY) ?? '').toBe('');
    expect(mockStorage.kv.get(LAST_SERVER_KEY)).toBe(origin);
    expect(JSON.parse(mockStorage.kv.get(guestKey(origin)) ?? '{}').id).toBe(guestId);
  });

  it('sign out still signs out when the server call fails', async () => {
    const server = signedInApi({
      'GET /api/me': me,
      'DELETE /api/auth/device-sessions/current': { status: 500, body: { error: 'boom' } },
    });
    await renderApp({ url: links.account(), api: server, signedIn });

    await fireEvent.press(screen.getByTestId(accountTestIds.signOut));
    await fireEvent.press(screen.getByTestId(accountTestIds.confirmSignOut));

    await waitFor(() => expect(screen.getByTestId(homeTestIds.root)).toBeOnTheScreen());
    expect(server.callsTo('DELETE /api/auth/device-sessions/current')).toHaveLength(1);
    expect(mockStorage.secure.get(SESSION_KEY) ?? '').toBe('');
  });

  it('a pending person sees the waiting message until /api/me says approved', async () => {
    await renderApp({
      url: links.account(),
      api: signedInApi({ 'GET /api/me': me }),
      signedIn: { ...signedIn, pending: true },
    });

    expect(screen.getByTestId(accountTestIds.pending)).toBeOnTheScreen();
    await waitFor(() => expect(screen.queryByTestId(accountTestIds.pending)).toBeNull());
  });
});
