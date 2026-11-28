import type { QueryClient } from '@tanstack/react-query';
import { CLIENT_HEADER, GUEST_HEADER } from '@slideworks/core/guest';
import type { KeyValueStore, Platform } from '@slideworks/core/platform';
import type { GuestIdReader } from '../guest/guestIdReader';
import { clearSignedInQueries } from '../queries/queryClient';
import type { SessionStore } from '../session/sessionStore';

// The app's platform for @slideworks/core (contract K1). Person requests go to the signed-in (or signing-in) server
// with its bearer; guest requests carry only that origin's guest id; no cookie ever rides along.

export type Connectivity = { isOffline(): boolean; onOnline(listener: () => void): () => void };

export type AppPlatformDeps = {
  session: SessionStore;
  guestId: GuestIdReader;
  kv: KeyValueStore;
  connectivity: Connectivity;
  queryClient: QueryClient;
  // Replaces the current route with Home.
  goHome: () => void;
};

function memoryStore(): KeyValueStore {
  const values = new Map<string, string>();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => void values.set(key, value),
    removeItem: (key) => void values.delete(key),
  };
}

export function createAppPlatform(deps: AppPlatformDeps): Platform {
  const { session, guestId, kv, connectivity, queryClient, goHome } = deps;
  const memory = memoryStore();
  const apiOrigin = () => session.snapshot().origin ?? '';

  return {
    persistentStore: () => kv,
    sessionStore: () => memory,
    isOffline: () => connectivity.isOffline(),
    onOnline: (listener) => connectivity.onOnline(listener),
    apiOrigin,
    authHeaders(): Record<string, string> {
      const secret = session.secretFor(apiOrigin());
      return secret ? { Authorization: `Bearer ${secret}` } : {};
    },
    guestHeaders(origin): Record<string, string> {
      const id = guestId(origin);
      return id ? { [CLIENT_HEADER]: 'app', [GUEST_HEADER]: id } : { [CLIENT_HEADER]: 'app' };
    },
    credentials: () => 'omit',
    // Any 401 on a person request: the device session is over. Guest ids are left alone.
    onUnauthorized() {
      session.clearSecret();
      clearSignedInQueries(queryClient);
      goHome();
    },
    // The app reads me.access instead.
    onPendingApproval: () => undefined,
  };
}
