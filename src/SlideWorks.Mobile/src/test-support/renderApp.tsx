import { renderRouter } from 'expo-router/testing-library';
import { resolve } from 'node:path';
import type { Href } from 'expo-router';
import { guestKey } from '../guest/guestStore';
import { LAST_EMAIL_KEY, LAST_SERVER_KEY, PENDING_KEY, SESSION_KEY } from '../session/sessionStore';
import type { FakeApi } from './fakeApi';
import { mockStorage } from './nativeMocks';

// Renders the real app/ routes (root layout, gate and all) at a URL, after seeding the storage the app reads at start:
// the device session (K9) and a guest id (K7). Await it.
export type RenderAppOptions = {
  url?: Href;
  api: FakeApi;
  signedIn?: { origin: string; email: string; secret: string; pending?: boolean } | false;
  guest?: { origin: string; id: string; lastUsedAt?: number };
};

const appDir = resolve(__dirname, '../../app');

export async function renderApp({ url = '/', api, signedIn = false, guest }: RenderAppOptions) {
  if (signedIn) {
    mockStorage.kv.set(LAST_SERVER_KEY, signedIn.origin);
    mockStorage.kv.set(LAST_EMAIL_KEY, signedIn.email);
    mockStorage.kv.set(PENDING_KEY, String(signedIn.pending ?? false));
    mockStorage.secure.set(
      SESSION_KEY,
      JSON.stringify({ origin: signedIn.origin, secret: signedIn.secret }),
    );
  }
  if (guest) {
    mockStorage.kv.set(
      guestKey(guest.origin),
      JSON.stringify({ id: guest.id, lastUsedAt: guest.lastUsedAt ?? Date.now() }),
    );
  }
  api.install();
  return renderRouter(appDir, { initialUrl: String(url) });
}
