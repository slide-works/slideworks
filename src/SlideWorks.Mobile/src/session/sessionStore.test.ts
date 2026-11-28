import type { KeyValueStore } from '@slideworks/core/platform';
import { describe, expect, it } from 'vitest';
import { createSessionStore, SESSION_KEY, type SyncSecureStore } from './sessionStore';

const origin = 'https://slideworks.app';
const secret = 'swd_abc';

// Storage that survives a "restart" (a new store over the same maps) and logs every write in order.
function storage(options: { failSecureWrites?: boolean } = {}) {
  const secureValues = new Map<string, string>();
  const kvValues = new Map<string, string>();
  const writes: string[] = [];
  const secure: SyncSecureStore = {
    getItem: (key) => secureValues.get(key) ?? null,
    setItem: (key, value) => {
      if (options.failSecureWrites) throw new Error('keychain locked');
      writes.push(`secure ${key}`);
      secureValues.set(key, value);
    },
    deleteItemAsync: async (key) => {
      if (options.failSecureWrites) throw new Error('keychain locked');
      secureValues.delete(key);
    },
  };
  const kv: KeyValueStore = {
    getItem: (key) => kvValues.get(key) ?? null,
    setItem: (key, value) => {
      writes.push(`kv ${key}`);
      kvValues.set(key, value);
    },
    removeItem: (key) => void kvValues.delete(key),
  };
  return { secure, kv, writes, secureValues, restart: () => createSessionStore({ secure, kv }) };
}

function signedIn(deps: ReturnType<typeof storage>, pending = false) {
  const store = deps.restart();
  store.beginSignIn(origin);
  store.setPendingSecret(secret);
  store.commit('sam@example.com', pending);
  return store;
}

describe('sessionStore', () => {
  it('commit writes the secret last', () => {
    const deps = storage();
    signedIn(deps);

    expect(deps.writes.at(-1)).toBe(`secure ${SESSION_KEY}`);
    expect(deps.writes.length).toBe(4);
    expect(deps.restart().snapshot()).toEqual({
      origin,
      email: 'sam@example.com',
      signedIn: true,
      pending: false,
    });
  });

  it('a secret is only returned for its own origin', () => {
    const deps = storage();
    const store = deps.restart();
    store.beginSignIn(origin);
    store.setPendingSecret(secret);
    expect(store.secretFor(origin)).toBe(secret);
    expect(store.secretFor('https://evil.example')).toBeNull();
    expect(store.snapshot().signedIn).toBe(false);

    store.commit('sam@example.com', false);
    expect(store.secretFor(origin)).toBe(secret);
    expect(store.secretFor('https://slideworks.app:8443')).toBeNull();
    expect(deps.restart().secretFor('https://evil.example')).toBeNull();
  });

  it('a crash before commit leaves the app signed out', () => {
    const deps = storage();
    const store = deps.restart();
    store.beginSignIn(origin);
    store.setPendingSecret(secret);

    const afterCrash = deps.restart();
    expect(afterCrash.snapshot().signedIn).toBe(false);
    expect(afterCrash.secretFor(origin)).toBeNull();
  });

  it('clearSecret keeps last server and email and never throws', () => {
    const deps = storage();
    const store = signedIn(deps);
    let notified = 0;
    store.subscribe(() => notified++);

    store.clearSecret();
    expect(store.snapshot()).toEqual({
      origin: null,
      email: null,
      signedIn: false,
      pending: false,
    });
    expect(store.secretFor(origin)).toBeNull();
    expect(notified).toBe(1);
    expect(deps.secureValues.get(SESSION_KEY) ?? '').toBe('');
    expect(deps.restart().snapshot().signedIn).toBe(false);
    expect([store.lastServer(), store.lastEmail()]).toEqual([origin, 'sam@example.com']);

    const broken = storage({ failSecureWrites: true });
    const brokenStore = createSessionStore({ secure: broken.secure, kv: broken.kv });
    expect(() => brokenStore.clearSecret()).not.toThrow();
  });

  it('pending survives a restart', () => {
    const deps = storage();
    const store = signedIn(deps, true);
    expect(deps.restart().snapshot().pending).toBe(true);

    store.setPending(false);
    expect(store.snapshot().pending).toBe(false);
    expect(deps.restart().snapshot().pending).toBe(false);
  });
});
