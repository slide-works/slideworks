import type { KeyValueStore } from '@slideworks/core/platform';

// The app's device session (contract K9). A pure module: it reads storage once, synchronously, when it is created, so
// the first render already knows which side of the gate to show. The secret lives only in secure storage, bound to the
// origin it was issued by, and is only ever handed out for that origin.

// expo-secure-store: synchronous get and set, asynchronous delete.
export type SyncSecureStore = {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  deleteItemAsync(key: string): Promise<void>;
};

export type SessionSnapshot = {
  origin: string | null;
  email: string | null;
  signedIn: boolean;
  pending: boolean;
};

export interface SessionStore {
  // Synchronous; read from storage when created. The same object until something changes.
  snapshot(): SessionSnapshot;
  // For useSyncExternalStore.
  subscribe(listener: () => void): () => void;
  // The committed or pending secret, only for its own origin.
  secretFor(origin: string): string | null;
  beginSignIn(origin: string): void;
  setPendingSecret(secret: string): void;
  // Writes the secure value last, so a crash part-way leaves the app signed out.
  commit(email: string, pending: boolean): void;
  // A later /api/me answer changes it.
  setPending(pending: boolean): void;
  abandon(): void;
  // Never throws; keeps the last server and email.
  clearSecret(): void;
  lastServer(): string | null;
  lastEmail(): string | null;
}

export const SESSION_KEY = 'slideworks.session';
export const LAST_SERVER_KEY = 'slideworks.lastServer';
export const LAST_EMAIL_KEY = 'slideworks.lastEmail';
export const PENDING_KEY = 'slideworks.pending';

type Committed = { origin: string; secret: string };
type SigningIn = { origin: string; secret: string | null };

function parseSession(raw: string | null): Committed | null {
  if (!raw) return null;
  try {
    const value: unknown = JSON.parse(raw);
    if (typeof value !== 'object' || value === null) return null;
    const { origin, secret } = value as Record<string, unknown>;
    if (typeof origin !== 'string' || typeof secret !== 'string' || !origin || !secret) return null;
    return { origin, secret };
  } catch {
    return null;
  }
}

export function createSessionStore(deps: {
  secure: SyncSecureStore;
  kv: KeyValueStore;
}): SessionStore {
  const { secure, kv } = deps;
  let committed = parseSession(secure.getItem(SESSION_KEY));
  let signingIn: SigningIn | null = null;
  const listeners = new Set<() => void>();

  const read = (): SessionSnapshot => ({
    origin: committed?.origin ?? signingIn?.origin ?? null,
    email: committed ? kv.getItem(LAST_EMAIL_KEY) : null,
    signedIn: committed !== null,
    pending: committed !== null && kv.getItem(PENDING_KEY) === 'true',
  });
  let current = read();

  const changed = () => {
    current = read();
    for (const listener of [...listeners]) listener();
  };

  return {
    snapshot: () => current,
    subscribe(listener) {
      listeners.add(listener);
      return () => void listeners.delete(listener);
    },
    secretFor(origin) {
      if (committed && committed.origin === origin) return committed.secret;
      if (signingIn && signingIn.origin === origin) return signingIn.secret;
      return null;
    },
    beginSignIn(origin) {
      signingIn = { origin, secret: null };
      changed();
    },
    setPendingSecret(secret) {
      if (!signingIn) throw new Error('setPendingSecret needs beginSignIn first.');
      signingIn = { ...signingIn, secret };
    },
    commit(email, pending) {
      if (!signingIn?.secret)
        throw new Error('commit needs beginSignIn and setPendingSecret first.');
      const session: Committed = { origin: signingIn.origin, secret: signingIn.secret };
      kv.setItem(LAST_SERVER_KEY, session.origin);
      kv.setItem(LAST_EMAIL_KEY, email);
      kv.setItem(PENDING_KEY, String(pending));
      secure.setItem(SESSION_KEY, JSON.stringify(session));
      committed = session;
      signingIn = null;
      changed();
    },
    setPending(pending) {
      kv.setItem(PENDING_KEY, String(pending));
      changed();
    },
    abandon() {
      signingIn = null;
      changed();
    },
    clearSecret() {
      committed = null;
      signingIn = null;
      // An empty value is no session, so the next start is signed out even if the delete below never lands.
      try {
        secure.setItem(SESSION_KEY, '');
      } catch {
        // The in-memory state is already signed out; the delete below is the second try.
      }
      try {
        secure.deleteItemAsync(SESSION_KEY).catch(() => undefined);
      } catch {
        // Never throws: clearing the secret is what a failure path calls.
      }
      changed();
    },
    lastServer: () => kv.getItem(LAST_SERVER_KEY),
    lastEmail: () => kv.getItem(LAST_EMAIL_KEY),
  };
}
