// The one seam between the core and its host (contract K1). The core never touches storage, connectivity, the origin or
// credentials directly: it asks platform() at call time. The SPA configures its web platform when its api/client.ts is
// first imported; the app configures its own at start-up. Until then the core runs on in-memory stores, online, against
// its own origin, with no headers and no-op handlers, so the core's own tests need no host.

export type KeyValueStore = {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
};

export type UnauthorizedReason = 'unauthorized' | 'session_ended';

export interface Platform {
  // web: localStorage; app: expo-sqlite kv-store (synchronous).
  persistentStore(): KeyValueStore;
  // web: sessionStorage; app: in memory.
  sessionStore(): KeyValueStore;
  // web: !navigator.onLine; app: NetInfo.
  isOffline(): boolean;
  // Returns the function that stops listening.
  onOnline(listener: () => void): () => void;
  // Origin of 'person' requests. web: ''; app: 'https://host[:port]', no trailing slash.
  apiOrigin(): string;
  // web: {}; app: { Authorization: 'Bearer swd_…' } while a secret (or pending secret) is held for apiOrigin().
  authHeaders(): Record<string, string>;
  // web: {}; app: { 'X-SlideWorks-Client': 'app' } plus 'X-SlideWorks-Guest' when an id is held for that origin.
  guestHeaders(origin: string): Record<string, string>;
  // web: 'same-origin'; app: 'omit'.
  credentials(): RequestCredentials;
  // web: send to sign in; app: clear the secret and the signed-in queries, land on Home.
  onUnauthorized(reason: UnauthorizedReason): void;
  // web: send to /pending; app: no-op (the app reads me.access).
  onPendingApproval(): void;
}

function memoryStore(): KeyValueStore {
  const values = new Map<string, string>();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => void values.set(key, value),
    removeItem: (key) => void values.delete(key),
  };
}

export function defaultPlatform(): Platform {
  const persistent = memoryStore();
  const session = memoryStore();
  return {
    persistentStore: () => persistent,
    sessionStore: () => session,
    isOffline: () => false,
    onOnline: () => () => undefined,
    apiOrigin: () => '',
    authHeaders: () => ({}),
    guestHeaders: () => ({}),
    credentials: () => 'same-origin',
    onUnauthorized: () => undefined,
    onPendingApproval: () => undefined,
  };
}

let current: Platform = defaultPlatform();

export function configurePlatform(platform: Platform): void {
  current = platform;
}

export function platform(): Platform {
  return current;
}
